﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Netx.Loggine;

namespace Netx.Actor
{
    public class Actor : IActor
    {
        public const int Idle = 0;
        public const int Open = 1;
        public const int Disposed = 2;

        public const int SleepCmd = -99998983;


        public ActorScheduler ActorScheduler { get; }

        public IServiceProvider Container { get; }

        public ActorController @ActorController { get; }

        public Dictionary<int, ActorMethodRegister> CmdDict { get; }

        private readonly Lazy<ConcurrentQueue<ActorMessage>> actorRunQueue;

        public ConcurrentQueue<ActorMessage> ActorRunQueue { get => actorRunQueue.Value; }

        public ILog Log { get; }

        public int status = Idle;

        public IActorGet ActorGet { get; }

        public int Status => status;

        public int QueueCount => ActorRunQueue.Count;

        private readonly long maxQueuelen;
        public ActorOptionAttribute Option { get; }

        internal event EventHandler<IActorMessage>? CompletedEvent;

        public bool IsSleep { get; private set; } = true;

        private long lastRuntime = 0;

       
        /// <summary>
        /// 最后运行时间
        /// </summary>
        public long LastRunTime { get => lastRuntime; }

        /// <summary>
        /// 是否需要休眠
        /// </summary>
        public bool IsNeedSleep
        {
            get
            {
                if (IsSleep)
                    return false;

                return (Environment.TickCount - lastRuntime) > Option.Ideltime;

            }
        }


        public Actor(IServiceProvider container, IActorGet actorGet, ActorScheduler actorScheduler, ActorController instance)
        {
         
         
            this.ActorGet = actorGet;
            this.ActorController = instance;

            var options= instance.GetType().GetCustomAttributes<ActorOptionAttribute>(false);

            Option = new ActorOptionAttribute();

            foreach (var attr in options)
                if (attr is ActorOptionAttribute option)
                    Option = option;

            this.ActorScheduler = Option.SchedulerType switch
            {
                SchedulerType.None=> actorScheduler,
                SchedulerType.LineByLine=>ActorScheduler.LineByLine,
                SchedulerType.TaskFactory=> ActorScheduler.TaskFactory,
                SchedulerType.TaskRun=>ActorScheduler.TaskRun,
                _=> actorScheduler
            };


            maxQueuelen = Option.MaxQueueCount;

            ActorController.ActorGet = ActorGet;
            ActorController.Status = this;
            this.Container = container;
           
            actorRunQueue = new Lazy<ConcurrentQueue<ActorMessage>>();
            Log = new DefaultLog(container.GetRequiredService<ILoggerFactory>().CreateLogger($"Actor-{instance.GetType().Name}"));
            this.CmdDict = LoadRegister(instance.GetType());
            
        }


        private Dictionary<int, ActorMethodRegister> LoadRegister(Type instanceType)
        {
            Dictionary<int, ActorMethodRegister> registerdict = new Dictionary<int, ActorMethodRegister>();


            foreach (var ainterface in instanceType.GetInterfaces())
            {
                if (ainterface.GetCustomAttribute<Build>(true) != null)
                {                  
                    foreach (var method in ainterface.GetMethods())
                    {
                        var attrs = method.GetCustomAttributes(true);



                        List<TAG> taglist = new List<TAG>();
                        OpenAccess openAccess = OpenAccess.Public;
                        foreach (var attr in attrs)
                        {
                            if (attr is TAG attrcmdtype)
                                taglist.Add(attrcmdtype);
                            else if (attr is OpenAttribute access)
                                openAccess = access.Access;
                        }

                        if (taglist.Count > 0)
                        {

                            if (method.ReturnType == null||TypeHelper.IsTypeOfBaseTypeIs(method.ReturnType, typeof(Task)) || method.ReturnType == typeof(void))
                            {
                                var type = from xx in method.GetParameters()
                                           select xx.ParameterType;

                                var methodx = instanceType.GetMethod(method.Name, type.ToArray());

                                if (methodx != null)
                                {

                                    var openattr=  methodx.GetCustomAttribute<OpenAttribute>();

                                    if (openattr != null)
                                        openAccess = openattr.Access;

                                    foreach (var tag in taglist)
                                    {
                                        var sr = new ActorMethodRegister(instanceType, methodx, openAccess);

                                        if (!registerdict.ContainsKey(tag.CmdTag))
                                            registerdict.Add(tag.CmdTag, sr);
                                        else
                                        {
                                            Log.ErrorFormat("Register actor service {Name},cmd:{CmdTag} repeat", method.Name, tag.CmdTag);
                                            registerdict[tag.CmdTag] = sr;
                                        }
                                    }
                                }
                            }
                            else
                                Log.ErrorFormat("Register Actor Service Return Type Err:{Name},Use void, Task or Task<T>", method.Name);
                        }


                    }
                }
            }




            var methods = instanceType.GetMethods();
            foreach (var method in methods)
                if (method.IsPublic)
                {
                    var attrs = method.GetCustomAttributes(true);



                    List<TAG> taglist = new List<TAG>();
                    OpenAccess openAccess = OpenAccess.Public;
                    foreach (var attr in attrs)
                    {
                        if (attr is TAG attrcmdtype)
                            taglist.Add(attrcmdtype);
                        else if (attr is OpenAttribute access)
                            openAccess = access.Access;
                    }

                    if (taglist.Count > 0)
                    {

                        if (method.ReturnType == null|| TypeHelper.IsTypeOfBaseTypeIs(method.ReturnType, typeof(Task)) || method.ReturnType == typeof(void) )
                        {
                            foreach (var tag in taglist)
                            {
                                var sr = new ActorMethodRegister(instanceType, method, openAccess);

                                if (!registerdict.ContainsKey(tag.CmdTag))
                                    registerdict.Add(tag.CmdTag, sr);
                                else
                                {
                                    Log.ErrorFormat("Register actor service {Name},cmd:{CmdTag} repeat", method.Name, tag.CmdTag);
                                    registerdict[tag.CmdTag] = sr;
                                }
                            }
                        }
                        else
                            Log.ErrorFormat("Register Actor Service Return Type Err:{Name},Use void, Task or Task<T>", method.Name);
                    }
                }

            return registerdict;
        }




        public void Action(long id, int cmd, OpenAccess access, params object[] args)
        {

            if (status == Disposed)
                throw new ObjectDisposedException("this actor is dispose");

            if (maxQueuelen > 0)
                if (ActorRunQueue.Count > maxQueuelen)
                    throw new NetxException($"this actor queue count >{maxQueuelen}", ErrorType.ActorQueueMaxErr);

            var sa = new ActorMessage<object>(id, cmd, access, args);
            ActorRunQueue.Enqueue(sa);


            try
            {
                 Runing().Wait();
            }
            catch (Exception er)
            {
                Log.Error(er);
            }

        }

        public async ValueTask AsyncAction(long id, int cmd, OpenAccess access, params object[] args)
        {
            if (status == Disposed)
                throw new ObjectDisposedException("this actor is dispose");

            if (maxQueuelen > 0)
                if (ActorRunQueue.Count > maxQueuelen)
                    throw new NetxException($"this actor queue count >{maxQueuelen}", ErrorType.ActorQueueMaxErr);

            var sa = new ActorMessage<object>(id, cmd, access, args);            
            ActorRunQueue.Enqueue(sa);
            Runing().Wait();
            await sa.Awaiter;
        }

        public ValueTask<T> AsyncFunc<T>(long id, int cmd, OpenAccess access, params object[] args)
        {

            if (status == Disposed)
                throw new ObjectDisposedException("this actor is dispose");

            if (maxQueuelen > 0)
                if (ActorRunQueue.Count > maxQueuelen)
                    throw new NetxException($"this actor queue count >{maxQueuelen}", ErrorType.ActorQueueMaxErr);

            var sa = new ActorMessage<T>(id, cmd, access, args);
            ActorRunQueue.Enqueue(sa);
            Runing().Wait();
            return sa.Awaiter;

        }


        private Task  Runing()
        {
            if (Interlocked.Exchange(ref status, Open) == Idle)
            {
                async Task RunNext()
                {
                    try
                    {
                        while (ActorRunQueue.TryDequeue(out ActorMessage msg))
                        {
                            try
                            {
                                var res = await Call_runing(msg);

                                msg.Completed(res);

                                lastRuntime = Environment.TickCount;

                                if (CompletedEvent != null)
                                    if (msg.Cmd != SleepCmd)
                                    {
                                        msg.CompleteTime = TimeHelper.GetTime();
                                        CompletedEvent(ActorController, msg);
                                    }

                                if (status == Disposed)
                                    break;
                            }
                            catch (Exception er)
                            {
                                msg.SetException(er);
                            }
                        }
                    }                  
                    finally
                    {
                        Interlocked.CompareExchange(ref status, Idle, Open);
                    }
                };

               return ActorScheduler.Scheduler(RunNext);
            }

            return Task.CompletedTask;
        }

        private async Task<object?> Call_runing(ActorMessage result)
        {
            var cmd = result.Cmd;
            var args = result.Args;


            #region Awaken and sleep

            if (cmd == SleepCmd)
            {
                await ActorController.Sleeping();
                IsSleep = true;
                return default;
            }
            else if (IsSleep)
            {
                await ActorController.Awakening();
                IsSleep = false;
            }

            #endregion


            if (CmdDict.ContainsKey(cmd))
            {
                var service = CmdDict[cmd];

                if (result.Access>= service.Access)
                {

                    if (service.ArgsLen == args.Length)
                    {
                        ActorController.OrderTime = result.CompleteTime;

                        switch (service.ReturnMode)
                        {
                            case ReturnTypeMode.Null:
                                {
                                    ActorController.Runs__Make(cmd, args);
                                    return default;
                                }
                            case ReturnTypeMode.Task:
                                {
                                    await (dynamic)ActorController.Runs__Make(cmd, args);
                                    return default;
                                }
                            case ReturnTypeMode.TaskValue:
                                {
                                    return await (dynamic)ActorController.Runs__Make(cmd, args);
                                }
                            default:
                                {
                                    throw new NetxException("not found the return mode", ErrorType.ReturnModeErr);
                                }
                        }
                    }
                    else
                    {
                        return GetErrorResult($"actor cmd:{cmd} args count error", result.Id);
                    }
                }
                else
                {
                    throw new NetxException($"actor cmd:{cmd} permission denied", ErrorType.PermissionDenied);                   
                }
            }
            else
            {
                return GetErrorResult($"not found actor cmd:{cmd}", result.Id);              
            }           
        }

        public object GetErrorResult(string msg, long id)
        {
            Result err = new Result()
            {
                ErrorMsg = msg,
                Id = id,
                ErrorId = (int)ErrorType.ActorErr
            };

            return err;
        }


        public void Dispose()
        {
            if (Interlocked.Exchange(ref status, Disposed) != Disposed)
            {
                CmdDict.Clear();

                while (ActorRunQueue.Count>0)                
                    ActorRunQueue.TryDequeue(out _);                
            }           
        }
       
    }
}
