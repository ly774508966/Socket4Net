﻿using System;
using System.Net;
using socket4net.Service;

namespace socket4net.Net.TCP
{
    /// <summary>
    /// 终端
    /// 对服务器、客户端的抽象
    /// </summary>
    public interface IPeer
    {
        string Ip { get; }
        ushort Port { get; }
        IPAddress Address { get; }
        EndPoint EndPoint { get; }

        SessionMgr SessionMgr { get; }
        
        ILogicService LogicService { get; }
        INetService NetService { get; }

        bool IsLogicServiceShared { get; }
        bool IsNetServiceShared { get; }

        void Start(IService net, IService logic);
        void Stop();

        void PerformInLogic(Action action);
        void PerformInLogic<TParam>(Action<TParam> action, TParam param);

        void PerformInNet(Action action);
        void PerformInNet<TParam>(Action<TParam> action, TParam param);
    }

    public interface IPeer<TSession, out TLogicService, out TNetService> : IPeer
        where TSession : class, ISession, new()
        where TNetService : INetService, new()
        where TLogicService : ILogicService, new()
    {
        event Action<TSession, SessionCloseReason> EventSessionClosed;
        event Action<TSession> EventSessionEstablished;
        event Action EventPeerClosing;
    }
}