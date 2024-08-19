using System.Text.Json;
using LittleRosie;

var testMan = new TestManager();
string lines = Console.ReadLine();
var result = new Result 
{
    Build = testMan.Build(lines),
};

if (result.Build.Status != StatusType.OK) 
{
    Exit();
}

result.Tests = testMan.Run();
Exit();

void Exit() 
{
    string serialized = JsonSerializer.Serialize(result);
    Console.WriteLine(serialized);
    Environment.Exit(0);
}