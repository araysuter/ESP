using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ESP.Presentation;
using UnityEditor;
using UnityEngine;

public static class VerifyNasaWeatherProfile
{
    [MenuItem("ESP/Verify NASA weather parser")]
    public static void Run()
    {
        var valid = new DateTime(2026,11,14,9,0,0,DateTimeKind.Utc);
        string metadata="time { String minimum \"00z14nov2026\"; String maximum \"09z14nov2026\"; String grads_step \"180mn\"; }";
        NasaWeatherParser.Metadata(metadata,valid.AddHours(1),out var selected,out int index);
        if(selected!=valid || index!=3) throw new Exception("Time selection failed");
        bool stale=false; try { NasaWeatherParser.Metadata(metadata,valid.AddHours(13),out _,out _); } catch(FormatException) { stale=true; } if(!stale) throw new Exception("Stale time accepted");
        var sb=new StringBuilder("h, [1][72][1][1]\n"); for(int i=0;i<72;i++) sb.AppendLine($"[0][{i}][0], {i+1}");
        sb.Append("time, [1]\n739935.375\nlev, [72]\n"); for(int i=1;i<=72;i++) { if(i>1) sb.Append(", "); sb.Append(i.ToString(CultureInfo.InvariantCulture)); }
        sb.Append("\nlat, [1]\n28.5\nlon, [1]\n-80.625\n"); string fixture=sb.ToString();
        if(NasaWeatherParser.Parse(fixture,"h",valid,28.5,-80.625).Length!=72) throw new Exception("Parser failed");
        bool bad=false; try { NasaWeatherParser.Parse(fixture,"h",valid,28.75,-80.625); } catch(FormatException) { bad=true; } if(!bad) throw new Exception("Location mismatch accepted");
        var d=new Dictionary<string,double[]>(); foreach(string n in new[]{"h","pl","t","u","v"}) d[n]=new double[72]; d["ps"]=new[]{101000d};
        for(int i=0;i<72;i++) { d["h"][i]=i*500; d["pl"][i]=100000*Math.Exp(-i*.08); d["t"][i]=250; d["u"][i]=2; d["v"][i]=-1; }
        d["t"][2]=1e15;
        if(NasaWeatherParser.Build(d,valid,28.5,-80.625).Levels.Count!=71) throw new Exception("Fill handling failed");
        Debug.Log("NASA weather parser verification passed (synthetic fixtures, no network)");
    }
}
