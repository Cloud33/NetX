﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;


public class MakeBne
{
    public ILProcessor Processor { get; set; }
    public MethodReference Method { get; set; }
    public int Cmd { get; set; }
    public Instruction Start { get; set; }
    public Instruction Next { get; set; }
    public ModuleWeaver ModuleWeaver { get; set; }

    public bool IsEnd { get; set; }
    public Instruction End { get; set; }

    public MakeBne(ILProcessor processor)
    {
        this.Processor = processor;
        Start = processor.Create(OpCodes.Ldarg_1);
        End = processor.Create(OpCodes.Ldnull);
    }

    public List<Instruction> MakeCode()
    {
        if (!IsEnd)
        {
            var codes = new List<Instruction>
            {
               Start,
               Processor.Create(OpCodes.Ldc_I4,Cmd),
               Processor.Create(OpCodes.Bne_Un_S,Next),
               Processor.Create(OpCodes.Ldarg_0)
            };

            int i = 0;
            foreach (var parmeter in Method.Parameters)
            {
                codes.Add(Processor.Create(OpCodes.Ldarg_2));
                codes.Add(Processor.Create(OpCodes.Ldc_I4, i));
                codes.Add(Processor.Create(OpCodes.Ldelem_Ref));
                ModuleWeaver.Convert(Processor, codes, ModuleWeaver.ModuleDefinition.ImportReference(typeof(object)), parmeter.ParameterType, true);
                i++;
            }

            codes.Add(Processor.Create(OpCodes.Call, Method));

            if (Method.ReturnType.Name != "Void")
                codes.Add(Processor.Create(OpCodes.Ret));
            else
            {
                codes.Add(End);
                codes.Add(Processor.Create(OpCodes.Ret));
            }

            int size = 0;
            foreach (var il in codes)
                size += il.GetSize();

            if (size > 120)
                codes[2] = Processor.Create(OpCodes.Bne_Un, Next);

            return codes;
        }
        else
        {
            var codes = new List<Instruction>
            {
               Start,
               Processor.Create(OpCodes.Ldc_I4,Cmd),
               Processor.Create(OpCodes.Bne_Un_S,End),
               Processor.Create(OpCodes.Ldarg_0)
            };

            int i = 0;
            foreach (var parmeter in Method.Parameters)
            {
                codes.Add(Processor.Create(OpCodes.Ldarg_2));
                codes.Add(Processor.Create(OpCodes.Ldc_I4, i));
                codes.Add(Processor.Create(OpCodes.Ldelem_Ref));
                ModuleWeaver.Convert(Processor, codes, ModuleWeaver.ModuleDefinition.ImportReference(typeof(object)), parmeter.ParameterType, true);
                i++;
            }




            codes.Add(Processor.Create(OpCodes.Call, Method));

            if (Method.ReturnType.Name != "Void")
                codes.Add(Processor.Create(OpCodes.Ret));

            int size = 0;
            foreach (var il in codes)
                size += il.GetSize();

            if (size > 120)
                codes[2] = Processor.Create(OpCodes.Bne_Un, End);

            codes.Add(End);
            codes.Add(Processor.Create(OpCodes.Ret));

            return codes;
        }
    }
}

