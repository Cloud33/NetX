﻿using Netx.Interface;
using Netx.Loggine;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources.Copy;

namespace Netx
{
    public abstract class NetxBase : INetxBuildInterface
    {

        protected IIds IdsManager { get; set; }

        /// <summary>
        /// 日记输出
        /// </summary>
        public ILog Log { get; protected set; }

        public NetxBase(ILog log, IIds idsManager)
        {
            Log = log;
            IdsManager = idsManager;
        }


        /// <summary>
        /// 用于存放异步调用时,结果反馈的回调
        /// </summary>
        private readonly Lazy<ConcurrentDictionary<long, ManualResetValueTaskSource<Result>>> asyncResultDict = new Lazy<ConcurrentDictionary<long, ManualResetValueTaskSource<Result>>>(() =>
          new ConcurrentDictionary<long, ManualResetValueTaskSource<Result>>(50, 50)
        , true);

        /// <summary>
        /// 用于存放异步调用时,结果反馈的回调
        /// </summary>
        protected ConcurrentDictionary<long, ManualResetValueTaskSource<Result>> AsyncResultDict => asyncResultDict.Value;

        /// <summary>
        /// 用来超时处理
        /// </summary>
        private readonly Lazy<ConcurrentQueue<RequestKeyTime>> requestOutTimeQueue = new Lazy<ConcurrentQueue<RequestKeyTime>>(true);

        protected ConcurrentQueue<RequestKeyTime> RequestOutTimeQueue => requestOutTimeQueue.Value;

        /// <summary>
        /// 调用超时时间
        /// </summary>
        public long RequestOutTime { get; protected set; } = 10000;

        /// <summary>
        /// 运行，不等待返回结果,不会阻止当前线程
        /// </summary>
        /// <param name="cmdTag">命令</param>
        /// <param name="args">参数</param>
        public void Action(int cmdTag, params object[] args)
        {
            SendAction(cmdTag, args);
        }


        /// <summary>
        /// 异步运行,调用此函数会Return当前线程,直到完成调用后使用通知线程运行接下去的上下文
        /// </summary>
        /// <param name="cmdTag"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public Task AsyncAction(int cmdTag, params object[] args)
        {
            return SendAsyncAction(cmdTag, IdsManager.MakeId, args);
        }


        /// <summary>
        /// 运行函数异步方式,调用此函数会Return当前线程,直到结果返回后使用返回结果线程运行接下去的上下文
        /// </summary>
        /// <param name="cmdTag">命令</param>
        /// <param name="args">参数</param>
        /// <returns>返回结果</returns>
        public Task<IResult> AsyncFunc(int cmdTag, params object[] args)
        {
            return AsyncFuncSend(cmdTag, IdsManager.MakeId, args);
        }


        /// <summary>
        /// 运行函数异步方式,调用此函数会Return当前线程,直到结果返回后使用返回结果线程运行接下去的上下文,泛型方式
        /// </summary>
        /// <typeparam name="S">泛型类型</typeparam>
        /// <param name="cmdtag">命令</param>
        /// <param name="args">参数</param>
        /// <returns>返回结果</returns>
        public async Task<T> AsyncFunc<T>(int cmdtag, params object[] args)
        {
            var res = await AsyncFunc(cmdtag, args);
            return res.As<T>();
        }



        /// <summary>
        /// 运行调用,同步等待发送到服务器,不等待运行结束
        /// </summary>
        /// <param name="cmdTag">命令</param>
        /// <param name="args">需要发送参数</param>
        protected abstract void SendAction(int cmdTag, object[] args);

        /// <summary>
        /// 运行调用,同步等待结束
        /// </summary>
        /// <param name="cmdTag"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected abstract Task SendAsyncAction(int cmdTag, long Id, object[] args);


        /// <summary>
        /// 运行函数异步方式
        /// </summary>
        /// <param name="cmdTag">命令</param>
        /// <param name="args">参数</param>
        /// <returns></returns>
        protected abstract Task<IResult> AsyncFuncSend(int cmdTag, long Id, object[] args);


        public object Func(int cmdTag, Type type, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 添加异步回调到表中
        /// </summary>
        /// <param name="Ids"></param>
        /// <returns></returns>
        protected virtual ManualResetValueTaskSource<Result> AddAsyncResult(long ids)
        {
            ManualResetValueTaskSource<Result> asyncResult = new ManualResetValueTaskSource<Result>();
            if (!AsyncResultDict.TryAdd(ids, asyncResult))
            {
                Log.InfoFormat("add async back have id:{ids}", ids);
                AsyncResultDict[ids] = asyncResult;
            }

            if (RequestOutTime > 0)
                RequestOutTimeQueue.Enqueue(new RequestKeyTime(ids, TimeHelper.GetTime()));

            return asyncResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Dispose_table(List<IMemoryOwner<byte>> memDisposableList)
        {
            if (memDisposableList.Count > 0)
            {
                foreach (var mem in memDisposableList)
                    mem.Dispose();
                memDisposableList.Clear();
            }
        }




        protected struct RequestKeyTime
        {
            public long Key { get; }

            public long Time { get; }

            public RequestKeyTime(long key, long time)
            {
                Key = key;
                Time = time;
            }
        }

    }
}
