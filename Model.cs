using System.ComponentModel;
using System.Text.Json.Serialization;
namespace LittleRosie;

public class BuildResult 
{
    [JsonPropertyName("status")]
    public StatusType Status {get; set;} = StatusType.OK;

    [JsonPropertyName("message")]
    public string Message {get; set;} = "";

    [JsonPropertyName("compilation_errors")]
    public CompileError[] CompilatioErrors {get; set;} = [];
}

public class CompileError
{
    [JsonPropertyName("filename")]
    public string Filename {get; set;} = "";

    [JsonPropertyName("message")]
    public string Message {get; set;} = "";

    [JsonPropertyName("line")]
    public int Line {get; set;} = 1;

    [JsonPropertyName("character")]
    public int Character {get; set;} = 1;
}

public class Submission 
{
    [JsonPropertyName("src_test")] 
    public SourceFile[] SourceCodeTest {get; set;} = [];

    [JsonPropertyName("src")] 
    public SourceFile[] Sources {get; set;} = [];
}

public class SourceFile
{
    [JsonPropertyName("filename")] 
    public string Filename {get; set;} = "";

    [JsonPropertyName("src")] 
    public string SourceCode {get; set;} = "";
}

public class TestResult
{
    [JsonPropertyName("passed")] 
    public bool Passed {get; set;} = false;

    [JsonPropertyName("name")] 
    public string Name {get; set;} = "";

    [JsonPropertyName("stack_trace")] 
    public string StackTrace {get; set;} = "";
}

public class Result 
{
    public BuildResult Build {get; set;}
    public IEnumerable<TestResult> Tests {get; set;}
}

public enum StatusType 
{
    OK = 0,
    ERROR,
    INTERNAL_ERROR,
}