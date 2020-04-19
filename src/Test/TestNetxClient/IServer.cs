﻿using Netx;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TestNetxClient
{
  
    public enum CmdTagDef
    {
        AddActor=2000,
        Add=1000,
        RotueToActorAdd=1001,
        ClientAddOne=1003,
        ClientAdd=1004,
        Msg=3000,
        TestErr = 5000
    }

    /// <summary>
    /// 我们再客户端上面 定义一个 这样的接口,内容随便,关键是 TAG 和参数,你懂的
    /// </summary>
    [Build]
    public interface IServer
    {
        [TAG(CmdTagDef.AddActor)]
        Task<int> AddActor(int a, int b);

        [TAG(CmdTagDef.Add)]
        Task<int> Add(int a, int b);

        [TAG(CmdTagDef.RotueToActorAdd)]
        Task<int> RotueToAddActor(int a, int b);

        [TAG(CmdTagDef.ClientAdd)]
        Task<int> ClientAdd(int a, int b);

        [TAG(CmdTagDef.ClientAddOne)]
        Task<int> ClientAddOne(int a);

        [TAG(CmdTagDef.Msg)]
        void RunMsg(string msg);

        [TAG(1005)]
        Task<int> RecursiveTest(int a);

        [TAG(1007)]
        Task<bool> TestTimeOut();

        [TAG(1008)]
        Task<string> TestPermission();

        [TAG(4000)]
        Task<string> TestPermission2();

        [TAG(1009)]
        Task<List<string>> Testnull(Guid guid, string a, int b);

        [TAG(1006)]
        void Finsh();

        [TAG(1011)]
        Task<List<Guid>> TestMaxBuffer(List<Guid> data);

        [TAG(CmdTagDef.TestErr)]
        void TestErr();

        [TAG(5001)]
        void TestAsyncErr();
    }
}
