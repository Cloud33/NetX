﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Reflection;
namespace TestNetxServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var builtConfig = new ConfigurationBuilder()
            .AddJsonFile("logger.json")
            .Build();

            Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .ReadFrom.Configuration(builtConfig)
              .CreateLogger();


            var service = new Netx.Service.Builder.NetxServBuilder()
                .AddActorEvent<ActorEvent1>() //添加绑定事件1
                .AddActorEvent<ActorEvent2>() //添加绑定事件2
                .ConfigBase(p =>
                {
                    p.VerifyKey = "123123";
                    p.ClearSessionTime = 50000;
                })
                .RegisterService(Assembly.GetExecutingAssembly()) //注册当前DLL下面的所有控制器,包含RPC控制器和ACTOR控制器,我们都定义了一个
                .ConfigNetWork(p => //配置服务器SOCKET 方面的
                {
                    p.MaxConnectCout = 100;
                    p.Port = 1006;
                    p.MaxPackerSize = 256 * 1024;

                })
                .ConfigureLogSet(p =>
                {
                    p.ClearProviders();
                    p.AddSerilog();
                    // p.AddConfiguration(builtConfig.GetSection("Logging"));
                })
                .Build();


            service.Start(); //开始服务

            Console.ReadLine();

            Log.CloseAndFlush();
        }
    }
}
