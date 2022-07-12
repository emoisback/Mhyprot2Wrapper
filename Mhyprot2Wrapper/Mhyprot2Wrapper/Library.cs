using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Mhyprot2Wrapper
{
    public class Library
    {
        Mhyprot2DriverServices services = new Mhyprot2DriverServices();
        MhyProt2Connector mhyprot2Connector = new MhyProt2Connector();
        uint PID = 0;

        public bool IsConnectedToDriver = false;

        //ProcessBase
        IntPtr ProcessBaseAddress;

        public Library()
        {
            if(services.StartService())
            {
                if(mhyprot2Connector.OpenDrv())
                {
                    IsConnectedToDriver = true;
                    mhyprot2Connector.InitDrv((ulong)Process.GetCurrentProcess().Id);
                }
                else
                {
                    throw new Exception("Mhyprot2 service failed to start 1.");
                }
            }
            else
            {
                throw new Exception("Mhyprot2 service failed to start 2.");
            }
        }

        public bool CloseDriver()
        {
            mhyprot2Connector.CloseHandle();
            services.StopService();
            return true;
        }

        public bool OpenProcess(uint pid)
        {
            PID = pid;
            return true;
        }

        private IntPtr GetBaseAddress(string ModuleName)
        {
            List<MhyProtEnumModule> m = mhyprot2Connector.EnumProcessModule(PID);
            foreach (MhyProtEnumModule sm in m)
            {
                //Console.WriteLine("ModuleName: " + sm.ModuleName + " ModulePath:" + sm.ModulePath + " BaseAddress:0x" + sm.BaseAddress.ToString("x2") + " Size:0x" + sm.SizeOfImage.ToString("x2"));
                if (sm.ModuleName == ModuleName)
                { 
                    ProcessBaseAddress = sm.BaseAddress;
                    break;
                }
            }
            return ProcessBaseAddress;
        }

        private IntPtr GetRealAddress(string code, int size = 16)
        {
            code = code.Replace(" ", String.Empty);

            string splitCode = "";

            if (code.Contains("+"))
                splitCode = code.Substring(code.IndexOf('+') + 1);

            byte[] memoryAddress = new byte[size];

            if (!code.Contains("+") && !code.Contains(","))
            {
                try
                {
                    return new IntPtr(Convert.ToInt64(code, 16));
                }
                catch
                {
                    return IntPtr.Zero;
                }
            }

            if (splitCode.Contains(','))
            {
                List<Int64> offsetsList = new List<Int64>();
                string[] offsets = splitCode.Split(',');

                foreach (string offset in offsets)
                {
                    string test = offset;
                    if (offset.Contains("0x")) test = offset.Replace("0x", "");
                    Int64 preParse = 0;
                    if (!offset.Contains("-"))
                        preParse = Int64.Parse(test, NumberStyles.AllowHexSpecifier);
                    else
                    {
                        test = test.Replace("-", "");
                        preParse = Int64.Parse(test, NumberStyles.AllowHexSpecifier);
                        preParse = preParse * -1;
                    }
                    offsetsList.Add(preParse);
                }

                Int64[] offsetsInt64 = offsetsList.ToArray();
                IntPtr BasePointer = IntPtr.Zero;
                if (code.Contains("base") || code.Contains("main"))
                {
                    IntPtr readptr = Marshal.AllocHGlobal((IntPtr)size);
                    uint readResult = mhyprot2Connector.RWMemory( 0, PID, readptr, (IntPtr)((Int64)ProcessBaseAddress + offsetsInt64[0]), (uint)size);
                    var handle = GCHandle.Alloc(mhyprot2Connector.PtrToByte(readptr, readResult), GCHandleType.Pinned);
                    BasePointer = (IntPtr)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(IntPtr));
                    handle.Free();
                }
                else if (!code.Contains("base") && !code.Contains("main") && code.Contains("+"))
                {
                    string[] moduleName = code.Split('+');
                    IntPtr altModule = IntPtr.Zero;
                    if (!moduleName[0].ToLower().Contains(".dll") && !moduleName[0].ToLower().Contains(".exe") && !moduleName[0].ToLower().Contains(".bin"))
                    {
                        altModule = (IntPtr)Int64.Parse(moduleName[0], System.Globalization.NumberStyles.HexNumber);
                    }
                    else
                    {
                        List<MhyProtEnumModule> m = mhyprot2Connector.EnumProcessModule(PID);
                        foreach (MhyProtEnumModule sm in m)
                        {
                            //Console.WriteLine("ModuleName: " + sm.ModuleName + " ModulePath:" + sm.ModulePath + " BaseAddress:0x" + sm.BaseAddress.ToString("x2") + " Size:0x" + sm.SizeOfImage.ToString("x2"));
                            if (sm.ModuleName == moduleName[0])
                            {
                                altModule = sm.BaseAddress;
                                break;
                            }
                        }
                    }

                    IntPtr readptr = Marshal.AllocHGlobal((IntPtr)(uint)Marshal.SizeOf(typeof(IntPtr)));
                    var read = mhyprot2Connector.RWMemory( 0, PID, readptr, (IntPtr)((Int64)altModule + offsetsInt64[0]), (uint)Marshal.SizeOf(typeof(IntPtr)));
                    var handle = GCHandle.Alloc(mhyprot2Connector.PtrToByte(readptr, read), GCHandleType.Pinned);
                    BasePointer = (IntPtr)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(IntPtr));
                    handle.Free();
                }
                else
                {
                    IntPtr readptr = Marshal.AllocHGlobal((IntPtr)size);
                    uint readResult = mhyprot2Connector.RWMemory( 0, PID, readptr, (IntPtr)(offsetsInt64[0]), (uint)size);
                    var handle = GCHandle.Alloc(mhyprot2Connector.PtrToByte(readptr, readResult), GCHandleType.Pinned);
                    BasePointer = (IntPtr)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(IntPtr));
                    handle.Free();
                }

                IntPtr base1 = (IntPtr)0;

                for (int i = 1; i < offsetsInt64.Length; i++)
                {
                    base1 = new IntPtr(Convert.ToInt64((long)BasePointer + offsetsInt64[i]));

                    IntPtr readptr = Marshal.AllocHGlobal((IntPtr)size);
                    uint readResult = mhyprot2Connector.RWMemory( 0, PID, readptr, (IntPtr)(base1), (uint)size);
                    var handle = GCHandle.Alloc(mhyprot2Connector.PtrToByte(readptr, readResult), GCHandleType.Pinned);
                    BasePointer = (IntPtr)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(IntPtr));
                    handle.Free();
                }
                return base1;
            }
            else
            {
                Int64 trueCode = Convert.ToInt64(splitCode, 16);
                IntPtr altModule = IntPtr.Zero;
                if (code.Contains("base") || code.Contains("main"))
                {
                    altModule = ProcessBaseAddress;
                }
                else if (!code.Contains("base") && !code.Contains("main") && code.Contains("+"))
                {
                    string[] moduleName = code.Split('+');
                    if (!moduleName[0].ToLower().Contains(".dll") && !moduleName[0].ToLower().Contains(".exe") && !moduleName[0].ToLower().Contains(".bin"))
                    {
                        string theAddr = moduleName[0];
                        if (theAddr.Contains("0x")) theAddr = theAddr.Replace("0x", "");
                        altModule = (IntPtr)Int64.Parse(theAddr, NumberStyles.HexNumber);
                    }
                    else
                    {
                        try
                        {
                            List<MhyProtEnumModule> m = mhyprot2Connector.EnumProcessModule(PID);
                            foreach (MhyProtEnumModule sm in m)
                            {
                                //Console.WriteLine("ModuleName: " + sm.ModuleName + " ModulePath:" + sm.ModulePath + " BaseAddress:0x" + sm.BaseAddress.ToString("x2") + " Size:0x" + sm.SizeOfImage.ToString("x2"));
                                if (sm.ModuleName == moduleName[0])
                                {
                                    altModule = sm.BaseAddress;
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            //Debug.WriteLine("Module " + moduleName[0] + " was not found in module list!");
                            //Debug.WriteLine("Modules: " + string.Join(",", mProc.Modules));
                        }
                    }
                }
                else
                {
                    //altModule = GetModuleAddressByName(code.Split('+')[0]);

                    List<MhyProtEnumModule> m = mhyprot2Connector.EnumProcessModule(PID);
                    foreach (MhyProtEnumModule sm in m)
                    {
                        //Console.WriteLine("ModuleName: " + sm.ModuleName + " ModulePath:" + sm.ModulePath + " BaseAddress:0x" + sm.BaseAddress.ToString("x2") + " Size:0x" + sm.SizeOfImage.ToString("x2"));
                        if (sm.ModuleName == code.Split('+')[0])
                        {
                            altModule = sm.BaseAddress;
                            break;
                        }
                    }
                }

                return (IntPtr)((Int64)altModule + trueCode);
            }
        }


        private byte[] Read(IntPtr Address, uint length)
        {
            IntPtr readptr = Marshal.AllocHGlobal((IntPtr)length);
            uint read = mhyprot2Connector.RWMemory(0, PID, readptr, Address, length);
            if (read == 0) throw new Exception("Read failed");
            return mhyprot2Connector.PtrToByte(readptr, read);
        }

        private uint Write(IntPtr Address, byte[] data)
        {
            IntPtr writeptr = mhyprot2Connector.ByteToPtr(data);
            uint write = mhyprot2Connector.RWMemory(1, PID, Address, writeptr, (uint)data.Length);
            if (write == 0) throw new Exception("Write failed");
            return write;
        }

        public T Read<T>(string code)
        {
            var Address = GetRealAddress(code);
            var size = (uint)Marshal.SizeOf(typeof(T));
            var data = Read(Address, size);
            return GetStructure<T>(data);
        }

        public void Write<T>(T input, string code)
        {
            var Address = GetRealAddress(code);
            int size = Marshal.SizeOf(input);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(input, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            Write(Address, arr);
        }

        public string ReadString(string code)
        {
            var Address = GetRealAddress(code);
            byte[] numArray = Read(Address, 255);
            var str = Encoding.Default.GetString(numArray);

            if (str.Contains('\0'))
                str = str.Substring(0, str.IndexOf('\0'));
            return str;
        }

        public string ReadUnicodeString(string code)
        {
            var Address = GetRealAddress(code);
            byte[] numArray = Read(Address, 255);
            var str = Encoding.Unicode.GetString(numArray);

            if (str.Contains('\0'))
                str = str.Substring(0, str.IndexOf('\0'));
            return str;
        }

        public static T GetStructure<T>(byte[] bytes)
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var structure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return structure;
        }

        public static T GetStructure<T>(byte[] bytes, int index)
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] tmp = new byte[size];
            Array.Copy(bytes, index, tmp, 0, size);
            return GetStructure<T>(tmp);
        }


    }
}
