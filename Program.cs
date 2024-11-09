using LittleRosie;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

var testMan = new TestManager();
if (args.Length == 0)
{
    return;
}

switch (args[0])
{
    case "build":
    {
        string input = Console.ReadLine();
        var tests = testMan.Build(input);
        string output = JsonSerializer.Serialize(tests);
        // Console.Clear();
        Console.WriteLine(output);
        break;
    }

    case "run":
    {
        var tests = testMan.Tests("main.dll");
        var res = JsonSerializer.Serialize(tests);
        Console.WriteLine(res);
        break;
    }
}