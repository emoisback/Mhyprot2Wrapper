# Mhyprot2Wrapper
I learn how to use vuln driver in C#, so i make this.

Wrapper for mhyprot2 vuln driver function to read / write memory, easy for beginner to make external game hack.

And i use how memory.dll style for read and write.

Example :

```C#
using System;
using System.Diagnostics;
using Mhyprot2Wrapper;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Library library = new Library();
            library.OpenProcess((uint)Process.GetProcessesByName("ProjectN-Win64-Shipping")[0].Id);
            //Write to memory
            library.Write<float>(700, "ProjectN-Win64-Shipping.exe+0636F5C8,0,A0,288,18C");
            //Read from memory
            var x = library.Read<float>("ProjectN-Win64-Shipping.exe+0636F5C8,0,A0,288,18C");
            Console.WriteLine("Movespeed : "+x);
            library.CloseDriver();
            Console.ReadLine();
        }
    }
}
```

Thanks to kagurazakasanae and erfg12
