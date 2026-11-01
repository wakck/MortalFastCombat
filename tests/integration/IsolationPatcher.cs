// TEST ONLY. Never deploy to the normal game. Changes are in-memory, before game startup.
using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
#if BEPINEX6
using BepInEx;
using BepInEx.Preloader.Core.Patching;
#endif

public static class IsolatedPaths
{
    public static string Root,Persistent;
    private static bool unity,core;
    public static void Initialize(string gameRoot)
    {
        Root=Path.GetFullPath(gameRoot);
        string marker=Path.Combine(Root,"isolation-root.txt");
        if(!File.Exists(marker) || File.ReadAllText(marker).Trim()!="COEXISTENCE_TEST_ONLY")
            Environment.FailFast("Isolation marker missing; refusing to start test game");
        Persistent=Path.Combine(Root,"sandbox/persistent");
        Directory.CreateDirectory(Persistent);
        Directory.CreateDirectory(Path.Combine(Root,"validation"));
        Note("INITIALIZE "+Persistent);
    }
    private static void Note(string text)
    {File.AppendAllText(Path.Combine(Root,"validation/isolation-preloader.log"),DateTime.UtcNow.ToString("O")+" "+text+Environment.NewLine);}
    private static void Return(MethodDefinition method,string value)
    {
        method.IsPInvokeImpl=false;method.ImplAttributes=MethodImplAttributes.IL|MethodImplAttributes.Managed;
        method.Body=new MethodBody(method);ILProcessor il=method.Body.GetILProcessor();
        if(value!=null)il.Emit(OpCodes.Ldstr,value);
        else if(method.ReturnType.FullName=="System.Boolean")il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);
    }
    public static void Apply(AssemblyDefinition assembly)
    {
        ModuleDefinition m=assembly.MainModule;
        if(m.Name=="UnityEngine.CoreModule.dll") {
            TypeDefinition app=m.GetType("UnityEngine.Application");
            bool found=false;
            foreach(MethodDefinition method in app.Methods) {
                if(method.Name=="get_persistentDataPath"){Return(method,Persistent);found=true;}
                if(method.Name=="get_temporaryCachePath")Return(method,Path.Combine(Root,"sandbox/cache"));
                if(method.Name=="get_isFocused" && File.Exists(Path.Combine(Root,"hotkey-virtual-input.flag"))){Return(method,null);method.Body.Instructions[0].OpCode=OpCodes.Ldc_I4_1;}
            }
            if(!found)Environment.FailFast("Persistent path getter not found");
            foreach(MethodDefinition method in m.GetType("UnityEngine.PlayerPrefs").Methods)
                if(method.Name=="SetInt" || method.Name=="SetString" || method.Name=="SetFloat" || method.Name=="DeleteKey" || method.Name=="DeleteAll" || method.Name=="Save")Return(method,null);
            unity=true;Note("PATCHED persistent/cache paths; PlayerPrefs writes disabled");
        }
        if(m.Name=="Mortal.Core.dll") {
            bool found=false;
            foreach(MethodDefinition method in m.GetType("Mortal.Core.SaveSystem").Methods)
                if(method.Name=="GetUserFolder"){Return(method,"coexist-test");found=true;}
            if(!found)Environment.FailFast("Save user folder getter not found");
            foreach(MethodDefinition method in m.GetType("Mortal.Core.SystemSettings").Methods)
                if(method.Name=="IsRunningOnSteamDeck")Return(method,null);
            core=true;Note("PATCHED isolated save user; desktop platform query");
        }
    }
    public static void Finish()
    {if(!unity || !core)Environment.FailFast("Isolation patch incomplete; test aborted");Note("READY: all isolation patches applied before game code");}
}

#if BEPINEX6
[PatcherPluginInfo("local.legendofmortal.coexist.isolation","Coexistence save isolation TEST ONLY","1.0.0")]
public sealed class IsolationPatcher : BasePatcher
{
    public override void Initialize(){IsolatedPaths.Initialize(Paths.GameRootPath);}
    [TargetAssembly("UnityEngine.CoreModule.dll")]
    [TargetAssembly("Mortal.Core.dll")]
    public void Patch(AssemblyDefinition assembly){IsolatedPaths.Apply(assembly);}
    public override void Finalizer(){IsolatedPaths.Finish();}
}
#else
public static class IsolationPatcher
{
    public static IEnumerable<string> TargetDLLs {get{return new string[]{"UnityEngine.CoreModule.dll","Mortal.Core.dll"};}}
    public static void Initialize(){IsolatedPaths.Initialize(BepInEx.Paths.GameRootPath);}
    public static void Patch(AssemblyDefinition assembly){IsolatedPaths.Apply(assembly);}
    public static void Finish(){IsolatedPaths.Finish();}
}
#endif
