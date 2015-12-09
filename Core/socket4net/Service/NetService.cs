﻿using System;
using System.Threading;

namespace socket4net
{
    /// <summary>
    /// 网络服务线程（网络数据读写）
    /// </summary>
    public class NetService : INetService
    {
        private BlockingCollection<IJob> _jobs;
        private Thread _wokerThread;
        private bool _stopping;

        public int Jobs { get { return _jobs.Count; } }
        public int ExcutedJobsPerSec { get; private set; }
        public int Capacity { get; set; }

        private int _period = 10;
        public int Period
        {
            get { return _period; }
            set { _period = value; }
        }

        public int ReadBytesPerSec { get; private set; }
        public int WriteBytesPerSec { get; private set; }
        public int ReadPackagesPerSec { get; private set; }
        public int WritePackagesPerSec { get; private set; }

        private int _previousCalcTime;
        private int _excutedJobsPerSec;

        private int _readBytesPerSec;
        private int _readPackagesPerSec;

        private int _writeBytesPerSec;
        private int _writePackagesPerSec;

        public void Start()
        {
            _jobs = new BlockingCollection<IJob>(Capacity);

            _wokerThread = new Thread(WorkingProcedure) {Name = "NetService"};
            _wokerThread.Start();

            Logger.Instance.Debug("Net service started!");
        }

        public void Stop(bool joinWorker = true)
        {
            _stopping = true;
            if(joinWorker)
                _wokerThread.Join();

            Logger.Instance.Debug("Net service stopped!");
        }

        public void Perform(Action action)
        {
            Enqueue(action);
        }

        public void Perform<T>(Action<T> action, T param)
        {
            Enqueue(action, param);
        }

        private void Enqueue<T>(Action<T> proc, T param)
        {
            _jobs.Add(new Job<T>(proc, param));
        }

        private void Enqueue(Action proc)
        {
            _jobs.Add(new Job(proc));
        }

        private void WorkingProcedure()
        {
            while (!_stopping)
            {
                try
                {
                    IJob job;
                    if (_jobs.TryTake(out job, Period))
                    {
                        job.Do();

                        CalcPerformance();
                    }
                }
                catch (Exception e)
                {
                    Logger.Instance.ErrorFormat("{0} : {1}", e.Message, e.StackTrace);
                }
            }

            try
            {
                IJob leftJob;
                while (_jobs.TryTake(out leftJob, Period * 10))
                {
                    leftJob.Do();
                }
            }
            catch (Exception e)
            {
                Logger.Instance.ErrorFormat("{0} : {1}", e.Message, e.StackTrace);
            }
        }

        public void OnReadCompleted(int len, ushort cnt)
        {
            _readBytesPerSec += len;
            ++_readPackagesPerSec;
        }

        public void OnWriteCompleted(int len)
        {
            _writeBytesPerSec += len;
            ++_writePackagesPerSec;
        }

        private void CalcPerformance()
        {
            var delta = (Environment.TickCount - _previousCalcTime) / 1000.0f;
            if (delta < 1.0f)
                ++_excutedJobsPerSec;
            else
            {
                ExcutedJobsPerSec = (int)(_excutedJobsPerSec / delta);
                WriteBytesPerSec = (int)(_writeBytesPerSec / delta);
                WritePackagesPerSec = (int)(_writePackagesPerSec / delta);
                ReadBytesPerSec = (int)(_readBytesPerSec / delta);
                ReadPackagesPerSec = (int)(_readPackagesPerSec / delta);

                _previousCalcTime = Environment.TickCount;
                _excutedJobsPerSec = 0;
                _writeBytesPerSec = 0;
                _writePackagesPerSec = 0;
                _readBytesPerSec = 0;
                _readPackagesPerSec = 0;
            }
        }
    }
}