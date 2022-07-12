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
