namespace DatabaseQuery;

public class ApiResponse
{
    public required string Type { get; set; }
    public required string ConnectionString { get; set; }
    public required string Query { get; set; }
}