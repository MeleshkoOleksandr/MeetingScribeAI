using System;

namespace MeetingScribe.Logic.Services;

public class DateHeader : LogListItem
{
    public DateTime Date { get; set; }
    public string DateLabel => Date.Date == DateTime.Today ? $"TODAY — {Date:MMMM dd, yyyy}" : Date.ToString("MMMM dd, yyyy");
}