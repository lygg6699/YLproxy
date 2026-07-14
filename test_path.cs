using System; 
using System.IO; 
using System.Reflection; 
using YLproxy.Utils; 
class Program 
{ 
    static void Main() 
    { 
        Console.WriteLine($\"Current Directory: {Environment.CurrentDirectory}\"); 
        Console.WriteLine($\"AppContext.BaseDirectory: {AppContext.BaseDirectory}\"); 
        Console.WriteLine($\"Assembly.GetEntryAssembly()?.Location: {Assembly.GetEntryAssembly()?.Location}\"); 
        Console.WriteLine($\"Assembly.GetExecutingAssembly().Location: {Assembly.GetExecutingAssembly().Location}\"); 
    } 
} 
