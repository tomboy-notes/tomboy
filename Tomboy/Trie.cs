using System;
using System.Collections.Generic;

namespace Tomboy
{
	public class TrieHit
	{
		public int Start { get; private set; }
		public int End { get; private set; }
		public string Key { get; private set; }
		public object Value { get; private set; }

		public TrieHit (int start, int end, string key, object value)
		{
			Start = start;
			End = end;
			Key = key;
			Value = value;
		}
	}

	public class TrieTree
	{
		class TrieState
		{
			public TrieState (char value, int depth, TrieState fail_state)
			{
				Value = value;
				Depth = depth;
				FailState = fail_state;
				Transitions = new LinkedList<TrieState> ();
			}

			public char Value { get; private set; }
			public int Depth { get; private set; }
			public TrieState FailState { get; set; }
			public LinkedList<TrieState> Transitions { get; private set; }

			public object Payload { get; set; }
		}

		readonly bool case_sensitive;
		readonly TrieState root;

		public TrieTree (bool case_sensitive)
		{
			this.case_sensitive = case_sensitive;
			root = new TrieState ('\0', -1, null);
		}

		public int MaxLength { get; private set; }

		public void AddKeyword (string keyword, object pattern_id)
		{
			if (!case_sensitive) {
				keyword = keyword.ToLower ();
			}

			var currentState = root;
			for (int i = 0; i < keyword.Length; i++) {
				char c = keyword[i];
				var targetState = FindStateTransition (currentState, c);
				if (targetState == null) {
					targetState = new TrieState (c, i, root);
					currentState.Transitions.AddFirst (targetState);
				}

				currentState = targetState;
			}
			currentState.Payload = pattern_id;

			MaxLength = Math.Max (MaxLength, keyword.Length);
		}

		public void ComputeFailureGraph ()
		{
			// Failure state is computed breadth-first (-> Queue)
			var state_queue = new Queue<TrieState> ();

			// For each direct child of the root state
			// * Set the fail state to the root state
			// * Enqueue the state for failure graph computing
			foreach (var transition in root.Transitions) {
				transition.FailState = root;
				state_queue.Enqueue (transition);
			}
			
			while (state_queue.Count > 0) {
				// Current state already has a valid fail state at this point
				var current_state = state_queue.Dequeue ();
				foreach (var transition in current_state.Transitions) {
					state_queue.Enqueue (transition);

					var failState = current_state.FailState;
					while ((failState != null) && FindStateTransition (failState, transition.Value) == null) {
						failState = failState.FailState;
					}

					if (failState == null) {
						transition.FailState = root;
					} else {
						transition.FailState = FindStateTransition (failState, transition.Value);   
					}                    
				}
			}
		}

		static TrieState FindStateTransition (TrieState state, char value)
		{
			if (state.Transitions == null)
			{
				return null;
			}
			foreach (var transition in state.Transitions) {
				if (transition.Value == value) {
					return transition;
				}
			}
			return null;
		}

		public IList<TrieHit> FindMatches (string haystack)
		{
			if (!case_sensitive) {
				haystack = haystack.ToLower ();
			}

			var current_state = root;

			var matches = new List<TrieHit> ();
			int start_index = 0;
			for (int i = 0; i < haystack.Length; i++) {
				var c = haystack [i];

				if (current_state == root) {
					start_index = i;
				}

				// While there's no matching transition, follow the fail states
				// Because we're potentially changing the depths (aka length of 
				// matched characters) in the tree we're updating the start_index
				// accordingly
				while ((current_state != root) && FindStateTransition (current_state, c) == null) {
					var old_state = current_state;                    
					current_state = current_state.FailState;
					start_index += old_state.Depth - current_state.Depth;
				}
				current_state = FindStateTransition (current_state, c) ?? root;

				// If the state contains a payload: We've got a hit
				// Return a TrieHit with the start and end index, the matched 
				// string and the payload object
				if (current_state.Payload != null) {
					var hit_length = i - start_index + 1;
					matches.Add(
						new TrieHit (start_index, start_index + hit_length, 
							haystack.Substring (start_index, hit_length), current_state.Payload));
				}
			}
			return matches;
		}
	}
}
