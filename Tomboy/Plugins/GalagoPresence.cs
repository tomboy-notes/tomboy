
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Mono.Unix;

using Gtk;
using Galago;

using Tomboy;

class GalagoManager
{
	TrieTree trie;

	public GalagoManager ()
	{
		Galago.Core.Init ("tomboy", false);

		///// 
		///// Connecting these cause crashes with the current 0.3.2 bindings...
		/////
		//Galago.Core.OnPersonAdded += OnPersonAdded;
		//Galago.Core.OnPersonRemoved += OnPersonRemoved;
		// This is just property change
		//Galago.Core.OnUpdated += OnUpdated;

		UpdateTrie (true);
	}

	public TrieTree Trie 
	{
		get { return trie; }
	}

	public event EventHandler PeopleChanged;
	public event EventHandler PresenceChanged;

	void OnUpdated (object sender, EventArgs args)
	{
		Console.WriteLine ("Got Presence Updated!");
		if (PresenceChanged != null)
			PresenceChanged (this, args);
	}

	void OnPersonAdded (object sender, Galago.AddedArgs args)
	{
		Console.WriteLine ("Person Added!");

		UpdateTrie (false);
		if (PeopleChanged != null)
			PeopleChanged (this, args);
	}

	void OnPersonRemoved (object sender, Galago.RemovedArgs args)
	{
		Console.WriteLine ("Person Removed!");

		UpdateTrie (false);
		if (PeopleChanged != null)
			PeopleChanged (this, args);
	}

	void UpdateTrie (bool refresh_query)
	{
		trie = new TrieTree (false /* !case_sensitive */);
		ArrayList people = new ArrayList ();

		Console.WriteLine ("Loading up the person trie, Part 1...");

		foreach (Person person in Galago.Core.GetPeople (false, refresh_query)) {
			string fname, mname, lname;
			person.GetProperty ("first-name", out fname);
			person.GetProperty ("middle-name", out mname);
			person.GetProperty ("last-name", out lname);

			if (person.DisplayName != null) {
				people.Add (new PersonLink (LinkType.PersonDisplayName, person));
			}

			// Joe
			if (fname != null) {
				people.Add (new PersonLink (LinkType.FirstName, person));
			}

			// Joe Smith & Smith Joe
			if (fname != null && lname != null) {
				people.Add (new PersonLink (LinkType.FirstLastName, person));
				people.Add (new PersonLink (LinkType.LastFirstName, person));
			}

			// Joe Michael Smith
			if (fname != null && mname != null && lname != null) {
				people.Add (new PersonLink (LinkType.FirstMiddleLastName, person));
			}

			foreach (Account account in person.GetAccounts(true)) {
				if (account.DisplayName != null) {
					people.Add (new PersonLink (LinkType.AccountDisplayName, 
								    account));
				}

				if (account.UserName != null &&
				    account.UserName != account.DisplayName) {
					people.Add (new PersonLink (LinkType.AccountUserName, 
								    account));
				}
			}
		}

		Console.WriteLine ("Loading up the person trie, Part 2...");

		foreach (PersonLink plink in people) {
			trie.AddKeyword (plink.LinkText, plink);
		}

		Console.WriteLine ("Done.");
	}
}

enum LinkType
{
	PersonDisplayName,
	AccountUserName,
	AccountDisplayName,
	FirstName,
	FirstLastName,
	LastFirstName,
	FirstMiddleLastName
}

class PersonLink
{
	LinkType link_type;
	Person   person;
	Account  account;

	public PersonLink (LinkType type, Person person)
	{
		this.link_type = type;
		this.person = person;
		this.account = null;

		Console.WriteLine ("Added person {0}: {1}", link_type, LinkText);
	}

	public PersonLink (LinkType type, Account account)
	{
		this.link_type = type;
		this.person = account.Person;
		this.account = account;		

		Console.WriteLine ("Added account {0}: {1}", link_type, LinkText);
	}

	public string LinkText
	{
		get {
			string fname, mname, lname;
			person.GetProperty ("first-name", out fname);
			person.GetProperty ("middle-name", out mname);
			person.GetProperty ("last-name", out lname);

			switch (link_type) {
			case LinkType.PersonDisplayName:
				return person.DisplayName;
			case LinkType.AccountUserName:
				return account.UserName;
			case LinkType.AccountDisplayName:
				return account.DisplayName;
			case LinkType.FirstName:
				return fname;
			case LinkType.FirstLastName:
				return fname + " " + lname;
			case LinkType.LastFirstName:
				return lname + " " + fname;
			case LinkType.FirstMiddleLastName:
				return fname + " " + mname + " " + lname;
			}
			return null;
		}
	}

	Account GetBestAccount ()
	{
		if (account != null)
			return account;

		if (person != null) {
			// BINDING BUG: Returns a Person instead of Account
			Person foo = person.PriorityAccount;
			Account best = new Account (foo.Handle);
			
			Console.WriteLine ("Using priority account '{0}' for {1}", 
					   best.UserName, 
					   LinkText);

			return best;
		}
		return null;
	}

	public void SendMessage ()
	{
		Account best = GetBestAccount ();
		if (best == null)
			throw new Exception ("No accounts associated with this person");

		Process p = new Process ();
		p.StartInfo.FileName = "gaim-remote";
		p.StartInfo.Arguments = 
			"uri " + 
			best.Service.Id + ":goim?screenname=" + best.UserName;
		p.StartInfo.UseShellExecute = false;

		p.Start ();
	}

	[DllImport("libgalago-gtk")]
	static extern IntPtr galago_gdk_pixbuf_new_from_presence (IntPtr presence,
								  int width,
								  int height,
								  int precedence);

	public Gdk.Pixbuf GetPresenceIcon ()
	{
		Account best = GetBestAccount ();
		if (best != null &&
		    best.Presence != null) {
			IntPtr icon = galago_gdk_pixbuf_new_from_presence (best.Presence.Handle,
									   16, 16,
									   4);
			if (icon != IntPtr.Zero)
				return new Gdk.Pixbuf (icon);
		}
		return null;
	}
}

class PersonTag : NoteTag
{
	GalagoManager galago;

	public PersonTag (string tag_name, GalagoManager galago)
		: base (tag_name)
	{
		this.galago = galago;
	}

	public override void Initialize (string element_name)
	{
		base.Initialize (element_name);

		Underline = Pango.Underline.Single;
		Foreground = "blue";
		CanSerialize = false;
		CanActivate = true;
	}
	
	protected override bool OnActivate (NoteEditor editor,
					    Gtk.TextIter start, 
					    Gtk.TextIter end)
	{
		string persona = start.GetText (end);
		PersonLink plink = (PersonLink) galago.Trie.Lookup (persona);

		try {
			plink.SendMessage ();
		} catch (Exception e) {
			string title = Catalog.GetString ("Cannot contact '{0}'");
			title = String.Format (title, persona);

			string message = Catalog.GetString ("Error running gaim-remote: {0}");
			message = String.Format (message, e.Message);

			Console.WriteLine (message);

 			HIGMessageDialog dialog = 
 				new HIGMessageDialog (editor.Toplevel as Gtk.Window,
 						      Gtk.DialogFlags.DestroyWithParent,
 						      Gtk.MessageType.Info,
 						      Gtk.ButtonsType.Ok,
 						      title,
 						      message);
 			dialog.Run ();
 			dialog.Destroy ();
		}

		return true;
	}
}

class GalagoPresencePlugin : NotePlugin 
{
	static GalagoManager galago;
	Gtk.TextTag person_tag;
	Gtk.TextTag link_tag;
	Gtk.TextTag url_tag;

	static GalagoPresencePlugin ()
	{
		galago = new GalagoManager ();
	}

	public GalagoPresencePlugin ()
	{
		// Do nothing.
	}

    	protected override void Initialize ()
	{
		person_tag = Note.TagTable.Lookup ("link:person");
		if (person_tag == null) {
			person_tag = new PersonTag ("link:person", galago);

			Console.WriteLine ("Adding link:person tag...");
			Note.TagTable.Add (person_tag);
		}

		link_tag = Note.TagTable.Lookup ("link:internal");
		url_tag = Note.TagTable.Lookup ("link:url");
	}

	protected override void Shutdown ()
	{
		galago.PeopleChanged -= OnPeopleChanged;
		galago.PresenceChanged -= OnPresenceChanged;
	}

	protected override void OnNoteOpened () 
	{
		galago.PeopleChanged += OnPeopleChanged;
		galago.PresenceChanged += OnPresenceChanged;

		Buffer.InsertText += OnInsertText;
		Buffer.DeleteRange += OnDeleteRange;

		// Highlight existing people in note
		HighlightInBlock (Buffer.StartIter, Buffer.EndIter);
	}

	void OnPeopleChanged (object sender, EventArgs args)
	{
		// Highlight people in note
		UnhighlightInBlock (Buffer.StartIter, Buffer.EndIter);
		HighlightInBlock (Buffer.StartIter, Buffer.EndIter);
	}

	void OnPresenceChanged (object sender, EventArgs args)
	{
		// Highlight people in note
		UnhighlightInBlock (Buffer.StartIter, Buffer.EndIter);
		HighlightInBlock (Buffer.StartIter, Buffer.EndIter);
	}

	void HighlightInBlock (Gtk.TextIter start, Gtk.TextIter end) 
	{
		foreach (TrieHit hit in galago.Trie.FindMatches (start.GetText (end))) {
			Gtk.TextIter match_start = 
				Buffer.GetIterAtOffset(start.Offset + hit.Start);
			
			// Don't create links inside note or URL links
			if (match_start.HasTag (url_tag) ||
			    match_start.HasTag (link_tag))
				continue;
			
			Gtk.TextIter match_end = match_start;
			match_end.ForwardChars (hit.End - hit.Start);

			Console.WriteLine ("Matching Person '{0}' at {1}-{2}...", 
					   hit.Key, 
					   hit.Start, 
					   hit.End);
			Buffer.ApplyTag (person_tag, match_start, match_end);
		}
	}

	void UnhighlightInBlock (Gtk.TextIter start, Gtk.TextIter end) 
	{
		Buffer.RemoveTag (person_tag, start, end);
	}

	void GetBlockExtents (ref Gtk.TextIter start, ref Gtk.TextIter end) 
	{
		// FIXME: Should only be processing the largest match string
		// size, so we don't slow down for large paragraphs 
		
		start.LineOffset = 0;
		end.ForwardToLineEnd ();
	}

	void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
	{
		Gtk.TextIter start = args.Start;
		Gtk.TextIter end = args.End;

		GetBlockExtents (ref start, ref end);
		
		UnhighlightInBlock (start, end);
		HighlightInBlock (start, end);
	}

	void OnInsertText (object sender, Gtk.InsertTextArgs args)
	{
		Gtk.TextIter start = args.Pos;
		start.BackwardChars (args.Length);

		Gtk.TextIter end = args.Pos;

		GetBlockExtents (ref start, ref end);

		UnhighlightInBlock (start, end);
		HighlightInBlock (start, end);
	}
}	
