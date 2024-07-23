using System.Text.Json;

if (args.Count() == 0) 
{
    Console.WriteLine("[]");
    return;
}

string command = args[0];
var testMan = new TestManager();
switch (command)
{
    case "build":
        string? lines = Console.ReadLine();
        var buildResult = testMan.Build(lines);
        string serialized = JsonSerializer.Serialize(buildResult);
        Console.WriteLine(serialized);
    break;

    case "execute":
        string testResult = testMan.Run();
        Console.WriteLine(testResult);
    break;
}