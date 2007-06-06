
using System;
using Tomboy;

namespace Tomboy.Tasks
{
	/// <summary>
	/// Contains all the task data, like the summary, details, etc.
	/// </summary>
	public class TaskData
	{
		readonly string uri;
		string summary;
		string details;
		DateTime create_date;
		DateTime last_change_date;
		DateTime due_date;
		DateTime completion_date;
		TaskPriority priority;
		string origin_note_uri;

		public TaskData (string uri)
		{
			this.uri = uri;
			this.details = string.Empty;
			
			create_date = DateTime.MinValue;
			last_change_date = DateTime.MinValue;
			due_date = DateTime.MinValue;
			completion_date = DateTime.MinValue;
			priority = TaskPriority.Undefined;
			origin_note_uri = string.Empty;
		}

		public string Uri
		{
			get { return uri; }
		}

		public string Summary
		{
			get { return summary; }
			set { summary = value; }
		}

		public string Details
		{
			get { return details; }
			set { details = value; }
		}

		public DateTime CreateDate
		{
			get { return create_date; }
			set { create_date = value; }
		}

		public DateTime LastChangeDate
		{
			get { return last_change_date; }
			set { last_change_date = value; }
		}
		
		public DateTime DueDate
		{
			get { return due_date; }
			set { due_date = value; }
		}
		
		public DateTime CompletionDate
		{
			get { return completion_date; }
			set { completion_date = value; }
		}
		
		public TaskPriority Priority
		{
			get { return priority; }
			set { priority = value; }
		}
		
		public string OriginNoteUri
		{
			get { return origin_note_uri; }
			set { origin_note_uri = value; }
		}
	}
}
