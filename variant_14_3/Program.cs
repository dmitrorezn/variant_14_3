using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace variant_14_3
{
    /*
     Напишіть консольний застосунок який буде очікувати від користувача число 
     - максимальна кількість потоків що дозволено запускати одночасно. 
     Необхідно 100 разів знайти всі прості числа менші за 10.000, 
     викорустовуючи цю кількість потоків та вивести у консоль загальний час виконання.
     Реалізувати за допомогою Task.
    */
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("Write:");

                int threadsNum = Convert.ToInt32(Console.ReadLine());

                // Sheduler Creation by castom TaskScheduler
                var scheduler = new BoundedThreadNumberTaskScheduler(threadsNum);
                var tasks = new List<Task>();

                // creatingg TaskFactory with created sheduler which will work whith our tasks in 
                // given by as threadsNum threads
                var factory = new TaskFactory(scheduler);

                // starting time
                var watch = Stopwatch.StartNew();

                for (int i = 0; i < 100; i++)
                {
                    tasks.Add(factory.StartNew(() => FindLessNums()));
                }

                // waiting all tasks reay to get clear time
                Task.WaitAll(tasks.ToArray());
                watch.Stop();

                Console.WriteLine("Time: " + watch.ElapsedMilliseconds + "ms");
            }
        }

        public static void FindLessNums()
        {
            for (int i = 1; i <= 100; i++)
            {
                Random rnd = new Random();
                int rundNum = rnd.Next(1, 20000);
                IsLessThan10K(rundNum);
            }
        }

        public static bool IsLessThan10K(int n)
        {
            if (n < 10000)
            {
                return true;
            }
            return false;
        }

    }

    public class BoundedThreadNumberTaskScheduler : TaskScheduler
    {
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        private readonly LinkedList<Task> tasks = new LinkedList<Task>();

        private readonly int _maxDegreeOfParallelism;

        private int _delegatesQueuedOrRunning = 0;

        public BoundedThreadNumberTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        protected sealed override void QueueTask(Task task)
        {
            lock (tasks)
            {
                tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                _currentThreadIsProcessingItems = true;
                try
                {
                    while (true)
                    {
                        Task item;
                        // lock tasks allows only one thread to work with it
                        lock (tasks)
                        {
                            if (tasks.Count == 0)
                            {
                                --_delegatesQueuedOrRunning;
                                break;
                            }
                            item = tasks.First.Value;
                            tasks.RemoveFirst();
                        }
                        base.TryExecuteTask(item);
                    }
                }
                finally
                {
                    _currentThreadIsProcessingItems = false;
                }
            }, null);
        }

        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (!_currentThreadIsProcessingItems) return false;

            if (taskWasPreviouslyQueued)
            {
                if (TryDequeue(task))
                    return base.TryExecuteTask(task);
                else
                    return false;
            }
            else
            {
                return base.TryExecuteTask(task);
            }
        }

        protected sealed override bool TryDequeue(Task task)
        {
            // lock tasks allows only one thread to work with it
            lock (tasks) return tasks.Remove(task);
        }
        public sealed override int MaximumConcurrencyLevel
        {

            get
            {
                return _maxDegreeOfParallelism;
            }

        }

        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(tasks, ref lockTaken);
                if (lockTaken) return tasks;
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(tasks);
            }
        }
    }
}