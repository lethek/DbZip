using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;


namespace DbZip.Threading
{

    public class SimpleMutex : IDisposable
    {

        public static bool Run(ThreadStart thread, int timeout = 0, string mutexId = null)
        {
            try {
                using (new SimpleMutex(timeout, mutexId)) {
                    thread.Invoke();
                }
                return true;
            } catch (TimeoutException) {
                return false;
            }
        }


        public SimpleMutex(int timeout = 0, string mutexId = null)
        {
            // Sub-classes can supply their own mutex-name generator and can defer to this base-class for the rest of initialization
            // ReSharper disable DoNotCallOverridableMethodsInConstructor
            string mutexName = GenerateMutexName(mutexId);
            // ReSharper restore DoNotCallOverridableMethodsInConstructor

            mutex = new Mutex(false, mutexName);

            var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(allowEveryoneRule);
            mutex.SetAccessControl(securitySettings);

            try {
                hasHandle = mutex.WaitOne(timeout < 0 ? Timeout.Infinite : timeout, false);
                if (hasHandle == false) {
                    throw new TimeoutException("Timeout waiting for exclusive access on mutex: " + mutexName);
                }

            } catch (AbandonedMutexException) {
                hasHandle = true;
            }
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (hasHandle && mutex != null) {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }


        /// <summary>
        /// Sub-classes can supply their own mutex-name generator and can defer to this base-class for the rest of initialization
        /// </summary>
        /// <param name="mutexId">A string to use for the mutex name. If the value is null, the returned name will be the assembly's GUID.</param>
        /// <returns>The name to use for the mutex.</returns>
        protected virtual string GenerateMutexName(string mutexId = null)
        {
            if (String.IsNullOrEmpty(mutexId)) {
                string appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value;
                return $"{{{appGuid}}}";
            }
            return mutexId;
        }


        private bool hasHandle;
        private Mutex mutex;

    }

}
