using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.DemoClient
{
    internal sealed class ThreadAssert
    {
        private readonly Thread MainThread;

        public ThreadAssert()
        {
            this.MainThread = Thread.CurrentThread;
        }

        public void Assert()
        {
            if (MainThread != Thread.CurrentThread)
            {
                throw new InvalidOperationException("Invalid calling thread");
            }
        }
    }
}
