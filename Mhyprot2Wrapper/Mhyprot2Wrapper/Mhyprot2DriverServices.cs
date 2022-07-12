using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mhyprot2Wrapper
{
    internal class Mhyprot2DriverServices
    {
        private IntPtr MhyProt2ServiceHandler { set; get; }

        public bool StartService()
        {
            try
            {
                IntPtr serviceHandle;
                //Check already services already Running
                if (ServiceHelper.OpenService(out serviceHandle, "mhyprot2", 0x0020 | 0x00010000))
                {
                    //Stop Service
                    ServiceHelper.StopService(serviceHandle);
                    ServiceHelper.DeleteService(serviceHandle);
                    ServiceHelper.CloseServiceHandle(serviceHandle);
                }

                //Check Mhyprot2 is exists on temp folder
                if (File.Exists(Environment.GetEnvironmentVariable("TEMP") + "\\mhyprot2.sys"))
                {
                    //Delete old mhyprot2.sys file
                    File.Delete(Environment.GetEnvironmentVariable("TEMP") + "\\mhyprot2.sys");
                }
                //Copy mhyprot2.sys if not exists on temp folder.
                File.Copy(Environment.CurrentDirectory + "\\mhyprot2.sys", Environment.GetEnvironmentVariable("TEMP") + "\\mhyprot2.sys");

                //Create Service if service is not running and previous service already stop
                var a = ServiceHelper.CreateService(
                    ref serviceHandle,
                    "mhyprot2", "mhyprot2",
                    Environment.GetEnvironmentVariable("TEMP") + "\\mhyprot2.sys",
                    (uint)NTAPI.SERVICE_ACCESS.SERVICE_ALL_ACCESS, 1/*SERVICE_KERNEL_DRIVER*/,
                    (uint)NTAPI.SERVICE_START.SERVICE_DEMAND_START, 1/*SERVICE_ERROR_NORMAL*/);
                //Start Service after create it
                ServiceHelper.StartService(serviceHandle);
                MhyProt2ServiceHandler = serviceHandle;
            }
            catch { return false; }
            return true;
        }

        public bool StopService()
        {
            try
            {
                ServiceHelper.StopService(MhyProt2ServiceHandler);
                ServiceHelper.CloseServiceHandle(MhyProt2ServiceHandler);
            }
            catch { return false; }
            return true;
        }
    }

    public static class ServiceHelper
    {
        public static bool CreateService(
            ref IntPtr hService,
            string ServiceName,
            string DisplayName,
            string BinPath,
            uint DesiredAccess,
            uint ServiceType,
            uint StartType,
            uint ErrorControl)
        {
            IntPtr hSCManager = NTAPI.OpenSCManager(0, 0, 0x0002/*SC_MANAGER_CREATE_SERVICE*/);

            if (hSCManager == IntPtr.Zero)
                return false;

            hService = NTAPI.CreateServiceW(
                hSCManager,
                ServiceName, DisplayName,
                DesiredAccess,
                ServiceType, StartType,
                ErrorControl, BinPath,
                0, 0, 0, 0, 0, 0);

            NTAPI.CloseServiceHandle(hSCManager);

            return hService != IntPtr.Zero;
        }
        public static bool OpenService(out IntPtr hService, string szServiceName, uint DesiredAccess)
        {
            IntPtr hSCManager = NTAPI.OpenSCManager(0, 0, DesiredAccess);
            hService = NTAPI.OpenService(hSCManager, szServiceName, DesiredAccess);
            NTAPI.CloseServiceHandle(hSCManager);
            return hService != IntPtr.Zero;
        }
        public static bool StopService(IntPtr hService)
        {
            NTAPI.SERVICE_STATUS ServiceStatus = new NTAPI.SERVICE_STATUS();
            return NTAPI.ControlService(hService, NTAPI.SERVICE_CONTROL.STOP, ref ServiceStatus);
        }

        public static bool StartService(IntPtr hService) => NTAPI.StartService(hService, 0, null);
        public static bool DeleteService(IntPtr hService) => NTAPI.DeleteService(hService);
        public static void CloseServiceHandle(IntPtr hService) => NTAPI.CloseServiceHandle(hService);

        /// <summary>
        /// Native functions :)
        /// </summary>
    }
}
