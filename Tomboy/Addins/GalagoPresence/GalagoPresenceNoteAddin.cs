
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Mono.Unix;

using Gtk;
using Galago;

using Tomboy;

namespace Tomboy.GalagoPresence
{

	class GalagoManager
	{
		TrieTree trie;

		public GalagoManager ()
		{
			try {
				Galago.Global.Init ("tomboy", Galago.InitFlags.Client);
			} catch (Exception e) {
				Logger.Error ("Error initializing Galago: " + e.ToString ());
				throw e;
			}
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
			get {
				return trie;
			}
		}

		public event EventHandler PeopleChanged;
		public event EventHandler PresenceChanged;

		void OnUpdated (object sender, EventArgs args)
		{
			Logger.Log ("Got Presence Updated!");
			if (PresenceChanged != null)
				PresenceChanged (this, args);
		}

		void OnPersonAdded (object sender, Galago.PersonAddedArgs args)
		{
			Logger.Log ("Person Added!");

			UpdateTrie (false);
			if (PeopleChanged != null)
				PeopleChanged (this, args);
		}

		void OnPersonRemoved (object sender, Galago.PersonRemovedArgs args)
		{
			Logger.Log ("Person Removed!");

			UpdateTrie (false);
			if (PeopleChanged != null)
				PeopleChanged (this, args);
		}

		void UpdateTrie (bool refresh_query)
		{
			trie = new TrieTree (false /* !case_sensitive */);
			List<PersonLink> people = new List<PersonLink> ();

			Logger.Log ("Loading up the person trie, Part 1...");

			foreach (Person person in Galago.Global.GetPeople (Galago.Origin.Remote,
			                refresh_query)) {
				string name = person.DisplayName;

				if (name != null) {
					people.Add (new PersonLink (LinkType.PersonDisplayName, person));
				}

				foreach (Account account in person.GetAccounts(true)) {
					if (account.DisplayName != null) {
						people.Add (new PersonLink (LinkType.AccountDisplayName,
						                            account));
					}

					if (account.Username != null &&
					                account.Username != account.DisplayName) {
						people.Add (new PersonLink (LinkType.AccountUserName,
						                            account));
					}
				}
			}

			Logger.Log ("Loading up the person trie, Part 2...");

			foreach (PersonLink plink in people) {
				trie.AddKeyword (plink.LinkText, plink);
			}

			Logger.Log ("Done.");
		}
	}

	enum LinkType
	{
		PersonDisplayName,
		AccountUserName,
		AccountDisplayName,
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

			Logger.Log ("Added person {0}: {1}", link_type, LinkText);
		}

		public PersonLink (LinkType type, Account account)
		{
			this.link_type = type;
			this.person = account.Person;
			this.account = account;

			Logger.Log ("Added account {0}: {1}", link_type, LinkText);
		}

		public string LinkText
		{
			get {

				switch (link_type) {
				case LinkType.PersonDisplayName:
					return person.DisplayName;
				case LinkType.AccountUserName:
					return account.Username;
				case LinkType.AccountDisplayName:
					return account.DisplayName;
				}
				return null;
			}
		}

		Account GetBestAccount ()
		{
			if (account != null)
				return account;

			if (person != null) {
				Account best = person.PriorityAccount;

				Logger.Log ("Using priority account '{0}' for {1}",
				            best.Username,
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
			        best.Service.Id + ":goim?screenname=" + best.Username;
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

				Logger.Log (message);

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


	public class GalagoPresenceNoteAddin : NoteAddin
	{
		static GalagoManager galago;
		Gtk.TextTag person_tag;
		Gtk.TextTag link_tag;
		Gtk.TextTag url_tag;

		static GalagoPresenceNoteAddin ()
		{
			galago = new GalagoManager ();
		}

		public GalagoPresenceNoteAddin ()
		{
			// Do nothing.
		}

		public override void Initialize ()
		{
			person_tag = Note.TagTable.Lookup ("link:person");
			if (person_tag == null) {
				person_tag = new PersonTag ("link:person", galago);

				Logger.Log ("Adding link:person tag...");
				Note.TagTable.Add (person_tag);
			}

			link_tag = Note.TagTable.Lookup ("link:internal");
			url_tag = Note.TagTable.Lookup ("link:url");
		}

		public override void Shutdown ()
		{
			galago.PeopleChanged -= OnPeopleChanged;
			galago.PresenceChanged -= OnPresenceChanged;
		}

		public override void OnNoteOpened ()
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

				Logger.Log ("Matching Person '{0}' at {1}-{2}...",
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

}
