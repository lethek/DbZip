using System;
using System.Threading;


namespace DbZip.Threading
{

    public class GlobalMutex : SimpleMutex
    {

        public static new bool Run(ThreadStart thread, int timeout = 0, string mutexId = null)
        {
            try {
                using (new GlobalMutex(timeout, mutexId)) {
                    thread.Invoke();
                }
                return true;
            } catch (TimeoutException) {
                return false;
            }
        }


        public GlobalMutex(int timeout = 0, string mutexId = null)
            : base(timeout, mutexId)
        {
        }


        /// <summary>
        /// Generates a mutex name in the global "namespace"
        /// </summary>
        /// <param name="mutexId">A string to use for the mutex name. If the value is null, the returned name will be the assembly's GUID (formatted for a global mutex).</param>
        /// <returns>The name to use for the global mutex.</returns>
        protected override string GenerateMutexName(string mutexId = null)
            => @"Global\" + base.GenerateMutexName(mutexId);
    }

}
