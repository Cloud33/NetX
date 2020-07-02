﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Netx;
using Netx.Actor;
using Netx.Loggine;
using System;

namespace TestNetxServer
{
    /// <summary>
    /// 我们可以定义一个事件类,用来接收所有Actor容器的完成事件
    /// 我们可以当Actor完成后保存他的 属性,以及当前的消息
    /// 把他们存入到数据库,那么 我们就可以事件回溯了
    /// </summary>
    public class ActorEvent1 : ActorEventBase
    {
        private ILog Log { get; }

        public ActorEvent1(IServiceProvider container)
        {
            Log = new DefaultLog(container.GetRequiredService<ILogger<ActorEvent1>>());
        }

        public override void ActorEventCompleted(object actorController, IActorMessage actorMessage)
        {
            Log.TraceFormat("PushTime:{time}  Cmd:{cmd} Completed", TimeHelper.GetTime(actorMessage.CompleteTime), actorMessage.Cmd);
        }
    }

    /// <summary>
    /// 我们可以绑定多个Actor事件
    /// </summary>
    public class ActorEvent2 : ActorEventBase
    {
        private ILog Log { get; }

        public ActorEvent2(IServiceProvider container)
        {
            Log = new DefaultLog(container.GetRequiredService<ILogger<ActorEvent2>>());
        }

        public override void ActorEventCompleted(object actorController, IActorMessage actorMessage)
        {
            switch (actorController)
            {
                case TestActorController testActorController:
                    {
                        Log.Trace($"I was called {testActorController.UseCount} times. ");
                    }
                    break;
            }

        }
    }
}
