﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Core.Log;
using Core.RPC;
using Core.Service;

#if !NET35
using System.Collections.Concurrent;
#else
using Core.ConcurrentCollection;
#endif

namespace Core.Net.TCP
{
    public interface IServer<TSession, out TLogicService, out TNetService> : IPeer<TSession>
        where TSession : class, ISession, new()
        where TNetService : IService, new()
        where TLogicService : IService, new()
    {
        TLogicService LogicService { get; }
        TNetService NetService { get; }
    }

    public class Server<TSession, TNetService, TLogicService> : IServer<TSession, TLogicService, TNetService>
        where TSession : class, ISession, new()
        where TNetService : class ,IService, new()
        where TLogicService : class ,IService, new()
    {
        public ushort Port { get; private set; }
        public string Ip { get; private set; }
        public IPAddress Address { get; private set; }
        public EndPoint EndPoint { get; private set; }

        public bool IsLogicServiceShared { get; private set; }
        public bool IsNetServiceShared { get; private set; }
        public TLogicService LogicService { get; private set; }
        public TNetService NetService { get; private set; }

        public SessionMgr SessionMgr { get; private set; }

        public event Action<TSession, SessionCloseReason> EventSessionClosed;
        public event Action<TSession> EventSessionEstablished;

        private Socket _listener;
        private readonly SessionFactory<TSession> _sessionFactory;

        private const int DefaultBacktrace = 10;
        private SocketAsyncEventArgs _acceptEvent;

        private ConcurrentQueue<Socket> _clients;
        private AutoResetEvent _socketAcceptedEvent;
        private Thread _sessionFactoryWorker;
        private bool _quit;

        public Server(string ip, ushort port)
        {
            Ip = ip;
            Port = port;

            Address = IPAddress.Parse(Ip);
            EndPoint = new IPEndPoint(Address, Port);

            _sessionFactory = new SessionFactory<TSession>();

            SessionMgr = new SessionMgr(this,
                session => { if (EventSessionEstablished != null)EventSessionEstablished(session as TSession); },
                (session, reason) => { if (EventSessionClosed != null)EventSessionClosed(session as TSession, reason); });
        }

        /// <summary>
        /// 启动
        /// 由于同一进程可以启动若干server，这些server可以
        /// 拥有自己独立的服务，亦可以共享服务
        /// </summary>
        /// <param name="net">网络收发服务</param>
        /// <param name="logic">逻辑处理服务</param>
        public void Start(IService net, IService logic)
        {
            _clients = new ConcurrentQueue<Socket>();

            _socketAcceptedEvent = new AutoResetEvent(false);
            _sessionFactoryWorker = new Thread(ProduceSessions);
            _sessionFactoryWorker.Start();

            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(EndPoint);
            _listener.Listen(DefaultBacktrace);

            _acceptEvent = new SocketAsyncEventArgs();
            _acceptEvent.Completed += OnAcceptCompleted;

            IsLogicServiceShared = (logic != null);
            IsNetServiceShared = (net != null);

            LogicService = (logic as TLogicService) ?? new TLogicService { Capacity = 10000, Period = 10 };
            if (!IsLogicServiceShared) LogicService.Start();

            NetService = (net as TNetService) ?? new TNetService { Capacity = 10000, Period = 10 };
            if (!IsNetServiceShared) NetService.Start();

            AcceptNext();
        }

        public void Stop()
        {
            SessionMgr.Dispose();

            _listener.Close();
            _acceptEvent.Dispose();

            _quit = true;
            _sessionFactoryWorker.Join();

            if (!IsLogicServiceShared) LogicService.Stop();
            if (!IsNetServiceShared) NetService.Stop();
        }

        public void PerformInLogic(Action action)
        {
            LogicService.Perform(action);
        }

        public void PerformInLogic<TParam>(Action<TParam> action, TParam param)
        {
            LogicService.Perform(action, param);
        }

        public void PerformInNet(Action action)
        {
            NetService.Perform(action);
        }

        public void PerformInNet<TParam>(Action<TParam> action, TParam param)
        {
            NetService.Perform(action, param);
        }

        private void ProduceSessions()
        {
            while (!_quit)
            {
                if (_socketAcceptedEvent.WaitOne())
                {
                    Socket sock;
                    while (_clients.TryDequeue(out sock))
                    {
                        var session = _sessionFactory.Create(sock, this);
                        session.Start();
                        SessionMgr.Add(session);
                    }
                }
            }
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                _clients.Enqueue(e.AcceptSocket);
                _socketAcceptedEvent.Set();
            }

            AcceptNext();
        }

        private void AcceptNext()
        {
            _acceptEvent.AcceptSocket = null;

            var result = true;
            try
            {
                // _listener在服务器stop时会抛出异常
                result = _listener.AcceptAsync(_acceptEvent);
            }
            catch (ObjectDisposedException e)
            {
                Logger.Instance.Error(e.Message);
            }
            catch (InvalidOperationException e)
            {
                Logger.Instance.Error(e.Message);
            }
            catch (Exception e)
            {
                Logger.Instance.Error(e.Message);
            }
            finally
            {
                if (!result)
                    HandleListenerError();
            }
        }

        private void HandleListenerError()
        {
            _listener.Close();
        }
    }

    public class Server : Server<RpcSession, NetService, LogicService>
    {
        public Server(string ip, ushort port) : base(ip, port)
        {
        }
    }
}
