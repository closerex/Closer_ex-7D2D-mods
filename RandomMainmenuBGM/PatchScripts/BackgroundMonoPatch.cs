using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;
using UnityEngine.Networking;

public class BackgroundMonoPatch
{
    public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

    public static FieldDefinition fld_bgm;
    public static FieldDefinition fld_as;
    public static FieldDefinition fld_running;
    //public static FieldDefinition fld_config_loaded;
    public static FieldDefinition fld_mod_path;
    //public static FieldDefinition fld_list_audio;
    //public static FieldDefinition fld_random;

    //public static MethodDefinition def_parse_configs;
    //public static MethodDefinition def_parse_file;
    //public static MethodDefinition def_get_random_clip;

    public static TypeDefinition type_modmanager;
    public static TypeDefinition type_dataloader;

    public static void Patch(AssemblyDefinition assembly)
    {
        Console.WriteLine("Applying Random Background Music Patch");
        ModuleDefinition module = assembly.MainModule;
        TypeDefinition def_bgmmono = module.Types.First(d => d.Name == "BackgroundMusicMono");

        /*
        fld_config_loaded = new FieldDefinition("bConfigLoaded",
            FieldAttributes.Public | FieldAttributes.Static,
            module.TypeSystem.Boolean);

        fld_list_audio = new FieldDefinition("listAudioClips",
            FieldAttributes.Public | FieldAttributes.Static,
            module.ImportReference(typeof(List<AudioClip>)));

        fld_random = new FieldDefinition("rnd",
            FieldAttributes.Public | FieldAttributes.Static,
            module.ImportReference(typeof(System.Random)));
        */
        fld_running = new FieldDefinition("bRunning",
            FieldAttributes.Public | FieldAttributes.Static,
            module.TypeSystem.Boolean);

        /*
        def_bgmmono.Fields.Add(fld_config_loaded);
        def_bgmmono.Fields.Add(fld_list_audio);
        def_bgmmono.Fields.Add(fld_random);
        */
        def_bgmmono.Fields.Add(fld_running);
        type_modmanager = module.Types.First(d => d.Name == "ModManager");
        type_dataloader = module.Types.First(d => d.Name == "DataLoader");

        fld_mod_path = type_modmanager.Fields.First(d => d.Name == "ModsBasePathLegacy");
        fld_mod_path.IsFamily = false;
        fld_mod_path.IsPrivate = false;
        fld_mod_path.IsPublic = true;
        fld_bgm = def_bgmmono.Fields.First(d => d.Name == "backgroundMusicClip");
        fld_as = def_bgmmono.Fields.First(d => d.Name == "audioSource");

        /*
        InsertStaticConstructor(module, def_bgmmono);
        InsertParseFile(module, def_bgmmono);
        InsertParseConfigs(module, def_bgmmono);
        InsertGetRandomClip(module, def_bgmmono);
        */
        PatchStart(module, def_bgmmono);
        PatchUpdate(module, def_bgmmono);
    }
    public static bool Link(ModuleDefinition gameModule, ModuleDefinition modModule)
    {
        return true;
    }
    /*
    public static void InsertStaticConstructor(ModuleDefinition module, TypeDefinition def_bgmmono)
    {
        TypeReference type_list_string = module.ImportReference(typeof(List<>));
        MethodReference ref_list_ctor = module.ImportReference(type_list_string.Resolve().Methods.First(d => d.Name == ".ctor" && !d.HasParameters));
        GenericInstanceType gen_type_string = new GenericInstanceType(type_list_string)
        {
            GenericArguments = { module.ImportReference(typeof(AudioClip)) }
        };
        ref_list_ctor.DeclaringType = gen_type_string;
        MethodReference ref_random_ctor = module.ImportReference(module.ImportReference(typeof(System.Random)).Resolve().Methods.First(d => d.Name == ".ctor" && !d.HasParameters));

        var methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static;
        var method = new MethodDefinition(".cctor", methodAttributes, module.ImportReference(typeof(void)));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Stsfld, fld_config_loaded));
        //method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        //method.Body.Instructions.Add(Instruction.Create(OpCodes.Stsfld, fld_busy));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Newobj, ref_list_ctor));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Stsfld, fld_list_audio));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Newobj, ref_random_ctor));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Stsfld, fld_random));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        def_bgmmono.Methods.Add(method);
    }
    public static void InsertGetRandomClip(ModuleDefinition module, TypeDefinition def_bgmmono)
    {
        TypeReference type_string = module.ImportReference(typeof(string));
        MethodReference ref_get_item = module.ImportReference(typeof(List<>).MakeGenericType(typeof(AudioClip)).GetMethod("get_Item"));
        //MethodReference ref_loadasset = module.ImportReference(type_dataloader.Methods.First(d => d.Name == "LoadAsset" && d.Parameters[0].ParameterType.FullName == type_string.FullName));
        //GenericParameter gen_param_loadasset = new GenericParameter("T", ref_loadasset);
        //GenericInstanceMethod gen_mtd_loadasset = new GenericInstanceMethod(ref_loadasset)
        //{
        //    GenericArguments = { module.ImportReference(typeof(AudioClip)) }
        //};

        MethodDefinition def = new MethodDefinition("getRandomClip", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static, module.ImportReference(typeof(AudioClip)));
        def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(AudioClip))));
        //i
        //def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(int))));
        //assetname
        //def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(string))));
        //rand
        def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(int))));
        //hit
        //def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(bool))));

        ILProcessor worker = def.Body.GetILProcessor();
        Instruction ret = worker.Create(OpCodes.Ldloc_0);
        //Instruction loop = worker.Create(OpCodes.Ldsfld, fld_list_audio);
        //Instruction add = worker.Create(OpCodes.Ldloc_1);
        //Instruction check = worker.Create(OpCodes.Ldloc_1);
        //Instruction checkhit = worker.Create(OpCodes.Ldloc, 4);

        def.Body.Instructions.Add(worker.Create(OpCodes.Ldnull));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldsfld, fld_list_audio));
        def.Body.Instructions.Add(worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(List<>).MakeGenericType(typeof(AudioClip)).GetMethod("get_Count"))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Blt, ret));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Ldstr, string.Empty));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldstr, "Random play"));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(Console).GetMethod(nameof(Console.WriteLine), new Type[] { typeof(string) }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldsfld, fld_random));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldsfld, fld_list_audio));
        def.Body.Instructions.Add(worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(List<>).MakeGenericType(typeof(AudioClip)).GetMethod("get_Count"))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(System.Random).GetMethod(nameof(System.Random.Next), new Type[] { typeof(int) }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldsfld, fld_list_audio));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Callvirt, ref_get_item));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_0));

        //def.Body.Instructions.Add(worker.Create(OpCodes.Dup));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_3));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_1));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_0));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Stloc, 4));
        /**
        def.Body.Instructions.Add(worker.Create(OpCodes.Br, check));
        //loop
        def.Body.Instructions.Add(loop);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Callvirt, ref_get_item));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldstr, "Trying to playing audio file: "));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(String).GetMethod(nameof(String.Concat), new Type[] { typeof(string), typeof(string) }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(Console).GetMethod(nameof(Console.WriteLine), new Type[] { typeof(string) }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, gen_mtd_loadasset));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Brtrue, ret));
        //add
        def.Body.Instructions.Add(add);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Add));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldsfld, fld_list_audio));
        def.Body.Instructions.Add(worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(List<>).MakeGenericType(typeof(string)).GetMethod("get_Count"))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Blt, check));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Br, check));
        //condition check
        def.Body.Instructions.Add(check);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_3));
        def.Body.Instructions.Add(worker.Create(OpCodes.Beq, checkhit));
        def.Body.Instructions.Add(worker.Create(OpCodes.Br, loop));
        //hit check
        def.Body.Instructions.Add(checkhit);
        def.Body.Instructions.Add(worker.Create(OpCodes.Brtrue, ret));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc, 4));
        def.Body.Instructions.Add(worker.Create(OpCodes.Br, loop));
        
        //return
        def.Body.Instructions.Add(ret);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ret));

        def_bgmmono.Methods.Add(def);
        def_get_random_clip = def;
    }


    public static void InsertParseConfigs(ModuleDefinition module, TypeDefinition def_bgmmono)
    {
        MethodDefinition def = new MethodDefinition("scanForConfigs", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static, module.ImportReference(typeof(void)));
        def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(int))));
        def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(string[]))));
        def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(string))));

        ILProcessor worker = def.Body.GetILProcessor();
        Instruction ret = worker.Create(OpCodes.Ret);
        Instruction check = worker.Create(OpCodes.Ldloc_0);
        Instruction loopstart = worker.Create(OpCodes.Ldloc_1);
        Instruction add = worker.Create(OpCodes.Ldloc_0);

        def.Body.Instructions.Add(worker.Create(OpCodes.Ldsfld, fld_mod_path));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(Directory).GetMethod(nameof(Directory.Exists)))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Brfalse, ret));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldsfld, fld_mod_path));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(Directory).GetMethod(nameof(Directory.GetDirectories), new Type[] { typeof(string) }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Br, check));
        //loop
        def.Body.Instructions.Add(loopstart);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldelem_Ref));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldstr, "/mainmenuMusic.ini"));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(String).GetMethod(nameof(String.Concat), new Type[] { typeof(string), typeof(string) }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(File).GetMethod(nameof(File.Exists)))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Brfalse, add));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, def_parse_file));
        //add loop condition
        def.Body.Instructions.Add(add);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Add));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_0));
        //check loop condition
        def.Body.Instructions.Add(check);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldlen));
        def.Body.Instructions.Add(worker.Create(OpCodes.Conv_I4));
        def.Body.Instructions.Add(worker.Create(OpCodes.Blt, loopstart));

        def.Body.Instructions.Add(ret);

        def_bgmmono.Methods.Add(def);
        def_parse_configs = def;
    }
    public static void InsertParseFile(ModuleDefinition module, TypeDefinition def_bgmmono)
    {
        TypeReference type_string = module.ImportReference(typeof(string));
        MethodReference ref_loadasset = module.ImportReference(type_dataloader.Methods.First(d => d.Name == "LoadAsset" && d.Parameters[0].ParameterType.FullName == type_string.FullName));
        GenericParameter gen_param_loadasset = new GenericParameter("T", ref_loadasset);
        GenericInstanceMethod gen_mtd_loadasset = new GenericInstanceMethod(ref_loadasset)
        {
            GenericArguments = { module.ImportReference(typeof(AudioClip)) }
        };

        MethodDefinition def = new MethodDefinition("parseAudioConfig", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static, module.ImportReference(typeof(void)));
        def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(int))));
        def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(string[]))));
        //last bundle path
        def.Body.Variables.Add(new VariableDefinition(type_string));
        //line
        def.Body.Variables.Add(new VariableDefinition(type_string));
        //is bundle
        def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(bool))));
        //assetname
        def.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(AudioClip))));
        def.Parameters.Add(new ParameterDefinition(type_string));

        ILProcessor worker = def.Body.GetILProcessor();
        Instruction ret = worker.Create(OpCodes.Ret);
        Instruction check = worker.Create(OpCodes.Ldloc_0);
        Instruction loopstart = worker.Create(OpCodes.Ldloc_1);
        Instruction add = worker.Create(OpCodes.Ldloc_0);
        Instruction valid = worker.Create(OpCodes.Ldloc, 4);
        Instruction asset = worker.Create(OpCodes.Ldloc_2);

        def.Body.Instructions.Add(worker.Create(OpCodes.Ldarg_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(File).GetMethod(nameof(File.Exists)))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Brfalse, ret));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldstr, string.Empty));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldarg_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(File).GetMethod(nameof(File.ReadAllLines), new Type[] { typeof(string) }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Br, check));
        //loop
        def.Body.Instructions.Add(loopstart);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldelem_Ref));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_3));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_3));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldlen));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Blt, add));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_3));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldstr, "#"));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(string).GetMethod(nameof(string.StartsWith), new Type[] { typeof(string) }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc, 4));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc, 4));
        def.Body.Instructions.Add(worker.Create(OpCodes.Brtrue, valid));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(string).GetMethod("get_Length"))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_0));
        def.Body.Instructions.Add(worker.Create(OpCodes.Bgt, valid));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ret));
        //valid bundle path
        def.Body.Instructions.Add(valid);
        def.Body.Instructions.Add(worker.Create(OpCodes.Brfalse, asset));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_3));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(string).GetMethod(nameof(string.Trim), new Type[] { }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_2));
        def.Body.Instructions.Add(worker.Create(OpCodes.Br, add));

        //load asset
        def.Body.Instructions.Add(asset);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldstr, "?"));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_3));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(string).GetMethod(nameof(string.Trim), new Type[] { }))));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, module.ImportReference(typeof(string).GetMethod(nameof(string.Concat), new Type[] { typeof(string), typeof(string), typeof(string) }))));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Stloc, 5));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Ldsfld, fld_list_audio));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc, 5));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(List<>).MakeGenericType(typeof(string)).GetMethod(nameof(List<string>.Add), new Type[] { typeof(string) }))));
        //def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc, 5));
        def.Body.Instructions.Add(worker.Create(OpCodes.Call, gen_mtd_loadasset));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc, 5));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc, 5));
        def.Body.Instructions.Add(worker.Create(OpCodes.Brfalse, add));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldsfld, fld_list_audio));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc, 5));
        def.Body.Instructions.Add(worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(List<>).MakeGenericType(typeof(AudioClip)).GetMethod(nameof(List<AudioClip>.Add), new Type[] { typeof(AudioClip) }))));
        //add
        def.Body.Instructions.Add(add);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldc_I4_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Add));
        def.Body.Instructions.Add(worker.Create(OpCodes.Stloc_0));
        //condition check
        def.Body.Instructions.Add(check);
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldloc_1));
        def.Body.Instructions.Add(worker.Create(OpCodes.Ldlen));
        def.Body.Instructions.Add(worker.Create(OpCodes.Conv_I4));
        def.Body.Instructions.Add(worker.Create(OpCodes.Blt, loopstart));

        def.Body.Instructions.Add(ret);

        def_bgmmono.Methods.Add(def);
        def_parse_file = def;
    }
    */

    public static void PatchStart(ModuleDefinition module, TypeDefinition def_bgmmono)
    {
        MethodDefinition def_Start = def_bgmmono.Methods.First(d => d.Name == "Start");
        FieldReference ref_fld_config_loaded = module.ImportReference(typeof(WWWHelper).GetField(nameof(WWWHelper.folder_parsed)));
        MethodReference ref_mtd_scan_folder = module.ImportReference(typeof(WWWHelper).GetMethod(nameof(WWWHelper.scanForMusicFolders)));
        MethodReference ref_mtd_can_random= module.ImportReference(typeof(WWWHelper).GetMethod(nameof(WWWHelper.canRandom)));
        MethodReference ref_mtd_get_random = module.ImportReference(typeof(WWWHelper).GetMethod(nameof(WWWHelper.getRandomPath)));
        MethodReference ref_mtd_get_audio = module.ImportReference(typeof(WWWHelper).GetMethod(nameof(WWWHelper.GetAudioClip)));

        ILProcessor worker = def_Start.Body.GetILProcessor();
        int index = def_Start.Body.Variables.Count;
        def_Start.Body.Variables.Add(new VariableDefinition(module.TypeSystem.String));

        Instruction insert = null;
        foreach(var ins in def_Start.Body.Instructions)
        {
            if (ins.OpCode == OpCodes.Callvirt && ins.Operand.ToString() == module.ImportReference(typeof(AudioSource).GetMethod("set_loop")).ToString())
            {
                ins.Previous.OpCode = OpCodes.Ldc_I4_0;
                insert = ins.Next;
                break;
            }
        }
        worker.InsertBefore(insert, worker.Create(OpCodes.Ldloc, index));
        worker.InsertBefore(insert, worker.Create(OpCodes.Ldlen));
        worker.InsertBefore(insert, worker.Create(OpCodes.Conv_I4));
        worker.InsertBefore(insert, worker.Create(OpCodes.Ldc_I4_1));
        worker.InsertBefore(insert, worker.Create(OpCodes.Blt, insert));
        //worker.InsertBefore(insert, worker.Create(OpCodes.Ldarg_0));
        worker.InsertBefore(insert, worker.Create(OpCodes.Ldc_I4_1));
        worker.InsertBefore(insert, worker.Create(OpCodes.Stsfld, fld_running));
        worker.InsertBefore(insert, worker.Create(OpCodes.Ldarg_0));
        worker.InsertBefore(insert, worker.Create(OpCodes.Ldloc, index));
        worker.InsertBefore(insert, worker.Create(OpCodes.Ldarg_0));
        worker.InsertBefore(insert, worker.Create(OpCodes.Ldfld, fld_as));
        worker.InsertBefore(insert, worker.Create(OpCodes.Call, ref_mtd_get_audio));
        worker.InsertBefore(insert, worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(MonoBehaviour).GetMethod(nameof(MonoBehaviour.StartCoroutine), new Type[] { typeof(IEnumerator) }))));
        worker.InsertBefore(insert, worker.Create(OpCodes.Pop));

        Instruction first = def_Start.Body.Instructions.First();
        Instruction random = worker.Create(OpCodes.Call, ref_mtd_can_random);

        worker.InsertBefore(first, worker.Create(OpCodes.Ldstr, String.Empty));
        worker.InsertBefore(first, worker.Create(OpCodes.Stloc, index));
        worker.InsertBefore(first, worker.Create(OpCodes.Ldsfld, ref_fld_config_loaded));
        worker.InsertBefore(first, worker.Create(OpCodes.Brtrue, random));
        worker.InsertBefore(first, worker.Create(OpCodes.Ldsfld, fld_mod_path));
        worker.InsertBefore(first, worker.Create(OpCodes.Call, ref_mtd_scan_folder));
        worker.InsertBefore(first, worker.Create(OpCodes.Ldc_I4_1));
        worker.InsertBefore(first, worker.Create(OpCodes.Stsfld, ref_fld_config_loaded));
        worker.InsertBefore(first, random);
        //worker.InsertBefore(first, worker.Create(OpCodes.Call, module.ImportReference(typeof(List<>).MakeGenericType(typeof(AudioClip)).GetMethod("get_Count"))));
        //worker.InsertBefore(first, worker.Create(OpCodes.Ldc_I4_1));
        //worker.InsertBefore(first, worker.Create(OpCodes.Blt, first));
        worker.InsertBefore(first, worker.Create(OpCodes.Brfalse, first));
        worker.InsertBefore(first, worker.Create(OpCodes.Call, ref_mtd_get_random));
        worker.InsertBefore(first, worker.Create(OpCodes.Stloc, index));
        
        //worker.InsertBefore(first, worker.Create(OpCodes.Brfalse, first));
        //worker.InsertBefore(first, worker.Create(OpCodes.Ldarg_0));
        //worker.InsertBefore(first, worker.Create(OpCodes.Ldloc, index));
        //worker.InsertBefore(first, worker.Create(OpCodes.Stfld, fld_bgm));

        /*
        Instruction last = def_Start.Body.Instructions.Last();
        worker.InsertBefore(last, worker.Create(OpCodes.Ldarg_0));
        worker.InsertBefore(last, worker.Create(OpCodes.Ldfld, fld_as));
        worker.InsertBefore(last, worker.Create(OpCodes.Ldc_I4_0));
        worker.InsertBefore(last, worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(AudioSource).GetMethod("set_loop"))));
        */
    }

    public static void PatchUpdate(ModuleDefinition module, TypeDefinition def_bgmmono)
    {
        MethodReference ref_mtd_can_random = module.ImportReference(typeof(WWWHelper).GetMethod(nameof(WWWHelper.canRandom)));
        MethodReference ref_mtd_get_random = module.ImportReference(typeof(WWWHelper).GetMethod(nameof(WWWHelper.getRandomPath)));
        MethodReference ref_mtd_get_audio = module.ImportReference(typeof(WWWHelper).GetMethod(nameof(WWWHelper.GetAudioClip)));

        TypeDefinition type_gamemanager = module.Types.First(d => d.Name == "GameManager");
        MethodDefinition def_Update = def_bgmmono.Methods.First(d => d.Name == "Update");
        int index = def_Update.Body.Variables.Count;
        def_Update.Body.Variables.Add(new VariableDefinition(module.TypeSystem.String));

        ILProcessor worker = def_Update.Body.GetILProcessor();


        for (int i = def_Update.Body.Instructions.Count - 1; def_Update.Body.Instructions[i - 1].OpCode != OpCodes.Ret; --i)
            def_Update.Body.Instructions.RemoveAt(i);
        //def_Update.Body.Instructions.Last().OpCode = OpCodes.Ret;
        //Instruction temp = def_Update.Body.Instructions.Last().Previous;
        //worker.InsertBefore(temp, worker.Create(OpCodes.Ldarg_0));
        //worker.InsertBefore(temp, worker.Create(OpCodes.Ldc_I4_0));
        //worker.InsertBefore(temp, worker.Create(OpCodes.Stsfld, fld_running));
		//Instruction jump = temp.Previous;
		//while(jump.OpCode != OpCodes.Ldarg_0)
		//	jump = jump.Previous;
		//Instruction condition = jump;
		//while(condition.OpCode != OpCodes.Brfalse_S)
		//	condition = condition.Previous;
		//condition.Operand = jump;
		
        Instruction start = def_Update.Body.Instructions.Last();
        Instruction ret = worker.Create(OpCodes.Ret);
		Instruction condition = worker.Create(OpCodes.Ldarg_0);
        //worker.InsertBefore(start, worker.Create(OpCodes.Ldarg_0));
        //worker.InsertBefore(start, worker.Create(OpCodes.Ldfld, fld_as));
		//worker.InsertBefore(start, worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(AudioSource).GetMethod("get_isPlaying"))));
        //worker.InsertBefore(start, worker.Create(OpCodes.Brtrue, ret));
        //worker.InsertBefore(start, worker.Create(OpCodes.Ldc_I4_0));
        //worker.InsertBefore(start, worker.Create(OpCodes.Stsfld, fld_running));

        Instruction[] instructions = new Instruction[]
        {
            //worker.Create(OpCodes.Call, module.ImportReference(type_gamemanager.Methods.Single(d => d.Name == "get_IsDedicatedServer"))),
            //worker.Create(OpCodes.Brtrue, start),
            worker.Create(OpCodes.Ldfld, fld_as),
            worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(AudioSource).GetMethod("get_isPlaying"))),
            worker.Create(OpCodes.Brfalse, condition),
            worker.Create(OpCodes.Ldsfld, fld_running),
            worker.Create(OpCodes.Brfalse, condition),
            worker.Create(OpCodes.Ldc_I4_0),
            worker.Create(OpCodes.Stsfld, fld_running),
            worker.Create(OpCodes.Ret),
            condition,
            worker.Create(OpCodes.Ldfld, fld_as),
            worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(AudioSource).GetMethod("get_isPlaying"))),
            worker.Create(OpCodes.Brtrue, ret),
            worker.Create(OpCodes.Ldsfld, fld_running),
            worker.Create(OpCodes.Brtrue, ret),
			//worker.Create(OpCodes.Ldc_I4_0),
			//worker.Create(OpCodes.Stsfld, fld_running),
            //worker.Create(OpCodes.Ldarg_0),
            //worker.Create(OpCodes.Ldsfld, fld_busy),
            //worker.Create(OpCodes.Brtrue, ret),
            worker.Create(OpCodes.Call, ref_mtd_can_random),
            //worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(List<>).MakeGenericType(typeof(AudioClip)).GetMethod("get_Count"))),
            //worker.Create(OpCodes.Ldc_I4_1),
            //worker.Create(OpCodes.Blt, ret),
            worker.Create(OpCodes.Brfalse, ret),
            worker.Create(OpCodes.Ldarg_0),
            worker.Create(OpCodes.Ldfld, fld_as),
            worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(AudioSource).GetMethod("get_volume"))),
            worker.Create(OpCodes.Ldc_R4, 0f),
            worker.Create(OpCodes.Ble_Un, ret),
            //worker.Create(OpCodes.Ldc_I4_1),
            //worker.Create(OpCodes.Stsfld, fld_busy),
            worker.Create(OpCodes.Call, ref_mtd_get_random),
            worker.Create(OpCodes.Stloc, index),
            //worker.Create(OpCodes.Ldc_I4_0),
            //worker.Create(OpCodes.Stsfld, fld_busy),
            worker.Create(OpCodes.Ldloc, index),
            worker.Create(OpCodes.Ldlen),
            worker.Create(OpCodes.Conv_I4),
            worker.Create(OpCodes.Ldc_I4_1),
            //worker.Create(OpCodes.Brfalse, ret),
            worker.Create(OpCodes.Blt, ret),
            //worker.Create(OpCodes.Ldarg_0),
            //worker.Create(OpCodes.Ldarg_0),
            worker.Create(OpCodes.Ldc_I4_1),
            worker.Create(OpCodes.Stsfld, fld_running),
            worker.Create(OpCodes.Ldarg_0),
            worker.Create(OpCodes.Ldloc, index),
            //worker.Create(OpCodes.Stfld, fld_bgm),
            worker.Create(OpCodes.Ldarg_0),
            worker.Create(OpCodes.Ldfld, fld_as),
            worker.Create(OpCodes.Call, ref_mtd_get_audio),
            worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(MonoBehaviour).GetMethod(nameof(MonoBehaviour.StartCoroutine), new Type[] { typeof(IEnumerator) }))),
            worker.Create(OpCodes.Pop),

        //worker.Create(OpCodes.Ldarg_0),
        //worker.Create(OpCodes.Ldfld, fld_bgm),
        //worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(AudioSource).GetMethod("set_clip"))),

            //worker.Create(OpCodes.Ldarg_0),
            //worker.Create(OpCodes.Ldfld, fld_as),
            //worker.Create(OpCodes.Callvirt, module.ImportReference(typeof(AudioSource).GetMethod(nameof(AudioSource.Play), new Type[] { }))),
            ret
        };
        foreach(var ins in instructions)
            worker.Append(ins);
    }
}

public static class WWWHelper
{
    public static void scanForMusicFolders(string path)
    {
        if (Directory.Exists(path))
        {
            string[] directories = Directory.GetDirectories(path);
            for (int i = 0; i < directories.Length; i++)
            {
                string text = directories[i] + "/music";
                Console.WriteLine("Scanning: " + text);
                if (Directory.Exists(text))
                {
                    scanForMusicFiles(text);
                }
            }
        }
    }
    public static void scanForMusicFiles(string dir)
    {
        string[] files = Directory.GetFiles(dir);
        for(int i = 0; i < files.Length; ++i)
        {
            string filename = files[i];
            string postfix = Path.GetExtension(filename);
            if (postfix.Length <= 0)
                Console.WriteLine("File extension does not exist: " + filename);
            else
            {
                list_path_music.Add(filename);
                Console.WriteLine("Found: " + filename);
            }
        }
    }
    public static bool canRandom()
    {
        return list_path_music.Count > 0;
    }
    public static string getRandomPath()
    {
        if (!canRandom())
            return string.Empty;
        string res;
        if (list_path_music.Count == 1)
            res = list_path_music[0];
        else
        {
            int rand;
            do
            {
                rand = rnd.Next(list_path_music.Count);
            } while (rand == _last_played);
            _last_played = rand;
            res = list_path_music[rand];
        }

        Console.WriteLine("Now playing: " + res);
        return res;
    }
    public static AudioType getAudioType(string path)
    {
        string postfix = Path.GetExtension(path).TrimStart('.');
        switch(postfix.ToLower())
        {
            case "mp3":
                return AudioType.MPEG;
            case "ogg":
                return AudioType.OGGVORBIS;
            case "wav":
                return AudioType.WAV;
            case "aiff":
            case "aif":
                return AudioType.AIFF;
            case "mod":
                return AudioType.MOD;
            case "it":
                return AudioType.IT;
            case "s3m":
                return AudioType.S3M;
            case "xm":
                return AudioType.XM;
            default:
                return AudioType.UNKNOWN;
        }
    }
    public static IEnumerator GetAudioClip(string fullPath, AudioSource audio)
    {
        using (var uwr = UnityWebRequestMultimedia.GetAudioClip(prefix + UnityWebRequest.EscapeURL(fullPath), getAudioType(fullPath)))
        {
            ((DownloadHandlerAudioClip)uwr.downloadHandler).streamAudio = true;

            yield return uwr.SendWebRequest();

            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError || uwr.result == UnityWebRequest.Result.DataProcessingError)
            {
                audio.Stop();
                yield break;
            }

            if (audio.volume <= 0f)
            {
                yield break;
            }

            DownloadHandlerAudioClip dlHandler = (DownloadHandlerAudioClip)uwr.downloadHandler;
            audio.clip = dlHandler.audioClip;
			audio.Play();
        }
    }

    private static List<string> list_path_music = new List<string>();
    private static System.Random rnd = new System.Random();
    private static string prefix = "file:///";
    private static int _last_played = -1;
    public static bool folder_parsed = false;
}

