using DiegoG.CLI;
using DiegoG.Utilities.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Daemon
{
    public static class MessageBoard
    {
        private static ConcurrentDictionary<string, Board> Boards_Dictionary { get; } = new();
        private static ConcurrentDictionary<string, Subscriber> Subscribers_Dictionary { get; } = new();

        public static ReadOnlyIndexedProperty<string, Board> Boards { get; } = new
        (n => Boards_Dictionary.GetOrAdd(n, s => new(s)));
        public static ReadOnlyIndexedProperty<string, Subscriber> Subscribers { get; } = new
        (n => Subscribers_Dictionary.GetOrAdd(n, s => new()));

        public class Board
        {
            private LinkedList<Subscriber> Subscribers { get; } = new();
            public string Name { get; init; }

            public bool RemoveSubscriber(Subscriber subscriber) => Subscribers.Contains(subscriber) && Subscribers.Remove(subscriber) && subscriber.Board_Unsubscribe(this);

            public void Post(CommandArguments msg)
            {
                foreach (var s in Subscribers)
                    s.Messages_Queue.Enqueue(msg);
            }
            public void PostAsync(CommandArguments msg)
            {
                if (Subscribers.Count > 10)
                    Task.Run(() => Post(msg));
                Post(msg);
            }

            internal bool AddSubscriber(Subscriber subscriber)
            {
                if (Subscribers.Contains(subscriber))
                    return false;
                Subscribers.AddLast(subscriber);
                return true;
            }
            internal Board(string name) => Name = name;
        }
        public class Subscriber
        {
            private LinkedList<Board> Subscriptions { get; } = new();
            internal ConcurrentQueue<CommandArguments> Messages_Queue { get; } = new();

            public IEnumerable<CommandArguments> Messages => Messages_Queue;
            public bool NextMessage(out CommandArguments msg) => Messages_Queue.TryDequeue(out msg);
            public bool Subscribe(Board board)
            {
                if (Subscriptions.Contains(board) && !board.AddSubscriber(this))
                    return false;
                Subscriptions.AddLast(board);
                return true;
            }
            public bool Unsubscribe(Board board) => Subscriptions.Contains(board) && Subscriptions.Remove(board) && board.RemoveSubscriber(this);
            internal bool Board_Unsubscribe(Board board) => Subscriptions.Contains(board) && Subscriptions.Remove(board);
            internal Subscriber() { }
        }
    }
}
