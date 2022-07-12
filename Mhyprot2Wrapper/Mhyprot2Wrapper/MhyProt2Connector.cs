using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mhyprot2Wrapper
{
    public enum MhyProt2Ctl : uint
    {
        DrvInit = 0x80034000,
        Mdl = 0x81004000,
        HeartBeat = 0x81014000,
        HeartBeat2 = 0x80024000,
        RWMemory = 0x81074000,
        EnumProcessList = 0x83014000,
        ListProcessModule = 0x81054000,
        Unk1 = 0x82004000,
        EnumDrivers = 0x82024000,
        KillProcess = 0x81034000
    }
    public struct RWMemory
    {
        public uint mode;
        public uint padding1;
        public uint TargetProcessID;
        public uint padding2;
        public IntPtr TargetProcessAddress;
        public IntPtr SourceProcessAddress;
        public uint BufferSize;
        public uint padding3;
    }

    public struct EnumDriver
    {
        public uint status;
        public uint count;
        public IntPtr Addr1;
        public IntPtr Addr2;
        public IntPtr Addr3;
    }
    public struct EnumProcess
    {
        public uint mode;
        public uint maxnum;
    }
    public struct EnumModule
    {
        public uint pid;
        public uint maxnum;
    }
    //680
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct MhyProtProcessList
    {
        public uint PID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ProcessName;
        private uint Padding1;
        public IntPtr EProcess;
        public IntPtr Padding2;
        public uint Is64Bit;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 132)]
        private byte[] Padding3;
    }
    //792
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct MhyProtEnumModule
    {
        public IntPtr BaseAddress;
        public uint SizeOfImage;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ModuleName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ModulePath;
        public uint Padding2;
    }
    public struct rand_mt64
    {
        public ulong[] array;
        public ulong index;
        public ulong decodeKey;
    };
    class MT64
    {
        private static ulong RAND_MT64_ARRAY_LEN = 312;
        public rand_mt64 mt;

        public MT64()
        {
            mt = new rand_mt64();
            mt.array = new ulong[RAND_MT64_ARRAY_LEN];
        }
        public void rand_mt64_init(ulong seed)
        {
            ulong f = 0x5851f42d4c957f2d;
            ulong prev_value = seed;
            mt.index = RAND_MT64_ARRAY_LEN;
            mt.array[0] = prev_value;
            for (ulong i = 1; i < RAND_MT64_ARRAY_LEN; i += 1)
            {
                prev_value = i + f * (prev_value ^ (prev_value >> 62));
                mt.array[i] = prev_value;
            }
        }

        public ulong rand_mt64_get()
        {
            ulong m = 156;
            ulong n = RAND_MT64_ARRAY_LEN;
            ulong[] mag01 = new ulong[2] { 0, 0xB5026F5AA96619E9 };
            ulong UM = 0xFFFFFFFF80000000;
            ulong LM = 0x7FFFFFFF;
            ulong x;

            if (mt.index >= n)
            {
                ulong i;

                for (i = 0; i < n - m; i += 1)
                {
                    x = (mt.array[i] & UM) | (mt.array[i + 1] & LM);
                    mt.array[i] = mt.array[i + m] ^ (x >> 1) ^
                        mag01[x & 0x1];
                }
                for (; i < n - 1; i += 1)
                {
                    x = (mt.array[i] & UM) | (mt.array[i + 1] & LM);
                    mt.array[i] = mt.array[i + (m - n)] ^ (x >> 1) ^
                        mag01[x & 0x1];
                }
                x = (mt.array[i] & UM) | (mt.array[0] & LM);
                mt.array[i] = mt.array[m - 1] ^ (x >> 1) ^
                    mag01[x & 0x1];

                mt.index = 0;
            }

            x = mt.array[mt.index];
            mt.index += 1;

            x ^= ((x >> 29) & 0x5555555555555555);
            x ^= ((x << 17) & 0x71D67FFFEDA60000);
            x ^= ((x << 37) & 0xFFF7EEE000000000);
            x ^= (x >> 43);

            return x;
        }
    }

    public unsafe class MhyProt2Connector
    {
        private IntPtr DriverHandler = IntPtr.Zero;
        public ulong seed;
        public ulong pid;
        private static MT64 m = new MT64();
        public bool isInit = false;
        private static ulong mt64res;
        private const string DriverDeviceName = "\\Device\\mhyprot2";
        public bool OpenDrv()
        {
            NTAPI.OBJECT_ATTRIBUTES objectAttributes = new NTAPI.OBJECT_ATTRIBUTES();
            NTAPI.UNICODE_STRING deviceName = new NTAPI.UNICODE_STRING(DriverDeviceName);
            NTAPI.IO_STATUS_BLOCK ioStatus;
            objectAttributes.Length = Marshal.SizeOf(typeof(NTAPI.OBJECT_ATTRIBUTES));
            objectAttributes.ObjectName = new IntPtr(&deviceName);

            uint status = 0;
            IntPtr deviceHandle;

            do
            {
                status = NTAPI.NtOpenFile(
                    &deviceHandle,
                    (uint)(NTAPI.ACCESS_MASK.GENERIC_READ | NTAPI.ACCESS_MASK.GENERIC_WRITE | NTAPI.ACCESS_MASK.SYNCHRONIZE),
                    &objectAttributes, &ioStatus, 0, 3/*OPEN_EXISTING*/);

                if (status != 0/*NT_SUCCESS*/)
                {
                    //Console.WriteLine($"[!] NtOpenFile failed! - {status:X}");
                    Thread.Sleep(250);
                    return false;
                }
            } while (status != 0/*NT_SUCCESS*/);

            DriverHandler = deviceHandle;
            return true;
        }

        public bool InitDrv(ulong pid)
        {
            if (DriverHandler == IntPtr.Zero) throw new Exception("Driver handle need to be open first");
            ulong seed = 0x233333333333;
            byte[] initdata = GenInitData(pid, seed);
            IntPtr lpinBuffer = ByteToPtr(initdata);
            IntPtr ret = Marshal.AllocHGlobal(8);
            ulong outlen = 0;
            bool res = NTAPI.DeviceIoControl(DriverHandler, (uint)MhyProt2Ctl.DrvInit, lpinBuffer, (uint)initdata.Length, ret, 8, &outlen, 0);
            if (!res) return res;
            ulong retmt64 = Marshal.PtrToStructure<ulong>(ret);
            return retmt64 == mt64res;
        }

        public List<MhyProtEnumModule> EnumProcessModule(uint pid)
        {
            EnumModule req = new EnumModule();
            req.pid = pid;
            req.maxnum = 300;
            byte[] reqdata = MhyEnCrypt(StructureToByte(req), 0x233333333333);
            IntPtr lpinBuffer = ByteToPtr(reqdata);
            IntPtr ret = Marshal.AllocHGlobal(301 * 792);
            ulong outlen = 0;
            bool res = NTAPI.DeviceIoControl(DriverHandler, (uint)MhyProt2Ctl.ListProcessModule, lpinBuffer, (uint)reqdata.Length, ret, 301 * 792, &outlen, 0);
            if (!res) throw new Exception("EnumProcessModule failed on pid: " + pid.ToString());
            byte[] retdata = MhyCrypt(PtrToByte(ret, (uint)outlen));
            uint count = BitConverter.ToUInt32(retdata, 0);
            //Console.WriteLine("Count: " + count.ToString());
            List<MhyProtEnumModule> modules = new List<MhyProtEnumModule>();
            for (int i = 0; i < count; i++)
            {
                byte[] singlemodule = new byte[792];
                Array.Copy(retdata, 4 + (i * 792), singlemodule, 0, 792);
                modules.Add(ByteToStructure<MhyProtEnumModule>(singlemodule));
            }
            return modules;
        }

        public uint RWMemory(uint mode, uint pid, IntPtr targetaddr, IntPtr sourceaddr, uint buffersize)
        {
            //mode = 0 : source=selected pid, target=self
            //mode = 1 : source=self, target=selected pid
            RWMemory req = new RWMemory();
            req.mode = mode;
            req.TargetProcessID = pid;
            req.TargetProcessAddress = targetaddr;
            req.SourceProcessAddress = sourceaddr;
            req.BufferSize = buffersize;
            //req.padding3 = 0x7ffb;
            byte[] reqdata = MhyEnCrypt(StructureToByte(req), 0x233333333333);
            IntPtr lpinBuffer = ByteToPtr(reqdata);
            IntPtr ret = Marshal.AllocHGlobal(12);
            ulong outlen = 0;
            bool res = NTAPI.DeviceIoControl(DriverHandler, (uint)MhyProt2Ctl.RWMemory, lpinBuffer, (uint)reqdata.Length, ret, 12, &outlen, 0);
            if (!res) throw new Exception("RWMemory failed on pid: " + pid.ToString());
            byte[] retdata = MhyCrypt(PtrToByte(ret, (uint)outlen));
            return BitConverter.ToUInt32(retdata, 0);
        }

        public bool KillProcess(uint pid)
        {
            byte[] reqdata = MhyEnCrypt(BitConverter.GetBytes(pid), 0x233333333333);
            IntPtr lpinBuffer = ByteToPtr(reqdata);
            IntPtr ret = Marshal.AllocHGlobal(12);
            ulong outlen = 0;
            bool res = NTAPI.DeviceIoControl(DriverHandler, (uint)MhyProt2Ctl.KillProcess, lpinBuffer, (uint)reqdata.Length, ret, 12, &outlen, 0);
            if (!res) throw new Exception("KillProcess failed on pid: " + pid.ToString());
            byte[] retdata = MhyCrypt(PtrToByte(ret, (uint)outlen));
            return BitConverter.ToUInt32(retdata, 0) == 0;
        }
        private void InitMt64()
        {
            m.rand_mt64_init(seed);
            int i = 7;
            do
            {
                mt64res = m.rand_mt64_get();
                //Console.WriteLine("MT64: " + mt64res.ToString("x2"));
            } while ((--i) != 0);
            isInit = true;
        }
        public byte[] GenInitData(ulong pid, ulong seed)
        {
            byte[] data = new byte[0x10];
            ulong PidData = 0xBAEBAEEC00000000 + pid;
            ulong LOW = seed ^ 0xEBBAAEF4FFF89042;
            ulong HIGH = seed ^ PidData;
            Array.Copy(BitConverter.GetBytes(HIGH), 0, data, 0, 8);
            Array.Copy(BitConverter.GetBytes(LOW), 0, data, 8, 8);
            this.seed = seed;
            InitMt64();
            return data;
        }

        public byte[] MhyEnCrypt(byte[] data, ulong ts)
        {
            m.mt.index = 0;
            m.mt.decodeKey = ts;
            byte[] endata = MT64Cryptor(data);
            byte[] ret = new byte[endata.Length + 8];
            Array.Copy(BitConverter.GetBytes(ts), ret, 8);
            Array.Copy(endata, 0, ret, 8, endata.Length);
            return ret;
        }
        public byte[] MhyCrypt(byte[] data)
        {
            ulong ts = BitConverter.ToUInt64(data, 0);
            byte[] endata = new byte[data.Length - 8];
            Array.Copy(data, 8, endata, 0, data.Length - 8);
            m.mt.index = 0;
            m.mt.decodeKey = ts;
            return MT64Cryptor(endata);
        }

        public byte[] MT64Cryptor(byte[] data)
        {
            byte[] ret = new byte[data.Length];
            int EncryptRound = data.Length >> 3;
            int i = 0;
            if (EncryptRound > 0)
            {
                ulong offset = 0;
                do
                {
                    ulong randNum = m.rand_mt64_get();
                    ulong v14 = m.mt.decodeKey + offset;
                    offset += 16;
                    ulong thisdata = BitConverter.ToUInt64(data, (i * 8));
                    ulong outdata = v14 ^ randNum ^ thisdata;
                    Array.Copy(BitConverter.GetBytes(outdata), 0, ret, (i * 8), 8);
                    m.mt.index %= 312;
                    ++i;
                } while (i < EncryptRound);
                return ret;
            }
            else
            {
                return data;
            }
        }

        /// <summary>
        /// 由结构体转换为byte数组
        /// </summary>
        public static byte[] StructureToByte<T>(T structure)
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] buffer = new byte[size];
            IntPtr bufferIntPtr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(structure, bufferIntPtr, true);
                Marshal.Copy(bufferIntPtr, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(bufferIntPtr);
            }
            return buffer;
        }

        /// <summary>
        /// 由byte数组转换为结构体
        /// </summary>
        public static T ByteToStructure<T>(byte[] dataBuffer)
        {
            object structure = null;
            int size = Marshal.SizeOf(typeof(T));
            IntPtr allocIntPtr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(dataBuffer, 0, allocIntPtr, size);
                structure = Marshal.PtrToStructure(allocIntPtr, typeof(T));
            }
            finally
            {
                Marshal.FreeHGlobal(allocIntPtr);
            }
            return (T)structure;
        }
        public byte[] PtrToByte(IntPtr ptr, uint length)
        {
            byte[] b = new byte[length];
            Marshal.Copy(ptr, b, 0, (int)length);
            Marshal.FreeHGlobal(ptr);
            return b;
        }
        public IntPtr ByteToPtr(byte[] data)
        {
            IntPtr ptr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);
            return ptr;
        }
        public bool CloseHandle()
        {
            return NTAPI.CloseHandle(DriverHandler);
        }
    }
}
