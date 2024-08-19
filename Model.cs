using System.Text.Json.Serialization;
namespace LittleRosie;

public class BuildResult 
{
    [JsonPropertyName("status")]
    public StatusType Status {get; set;}

    [JsonPropertyName("message")]
    public string Message {get; set;}

    [JsonPropertyName("compilation_errors")]
    public CompileError[] CompilatioErrors {get; set;}
}

public class CompileError
{
    [JsonPropertyName("filename")]
    public string Filename {get; set;}

    [JsonPropertyName("message")]
    public string Message {get; set;}

    [JsonPropertyName("line")]
    public int Line {get; set;}

    [JsonPropertyName("character")]
    public int Character {get; set;}
}

public class Submission 
{
    [JsonPropertyName("src_test")] 
    public string SourceCodeTest {get; set;}

    [JsonPropertyName("src")] 
    public SourceFile[] SourceFiles {get; set;}
}

public class SourceFile
{
    [JsonPropertyName("filename")] 
    public string Filename {get; set;}

    [JsonPropertyName("path")] 
    public string Path {get; set;}

    [JsonPropertyName("src")] 
    public string SourceCode {get; set;}
}

public class TestResult
{
    [JsonPropertyName("status")] 
    public string Status {get; set;}

    [JsonPropertyName("name")] 
    public string Name {get; set;}

    [JsonPropertyName("output")] 
    public string Output {get; set;}
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