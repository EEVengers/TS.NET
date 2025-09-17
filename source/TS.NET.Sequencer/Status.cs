namespace TS.NET.Sequencer;

public enum Status
{
    Running = 1,
    
    Passed = 2,
    Failed = 3,
    Done = 4,
    
    Skipped = 5,
    Error = 6,
    Cancelled = 7

    // If a step has limits that get evaluated, typical return is Passed/Failed
    // If a step executes code only, typical status is Done
    // Running/Skipped/Error/Cancelled applies to both
}
