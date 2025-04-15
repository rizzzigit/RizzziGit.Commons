using System.Diagnostics.CodeAnalysis;

namespace RizzziGit.Commons.Collections;

public static class ListExtensions
{
    extension<T>(List<T> list)
    {
        public bool TryShift([NotNullWhen(true)] out T? item)
        {
            if (list.Count > 0)
            {
                item = list[0]!;
                list.RemoveAt(0);
                return true;
            }
            else
            {
                item = default;
                return false;
            }
        }

        public T Shift()
        {
            T item = list[0];
            list.RemoveAt(0);
            return item;
        }

        public bool TryPop([NotNullWhen(true)] out T? item)
        {
            int count = list.Count;

            if (count > 0)
            {
                item = list[count - 1]!;
                list.RemoveAt(count - 1);

                return true;
            }
            else
            {
                item = default;
                return false;
            }
        }

        public T Pop()
        {
            int count = list.Count;

            T item = list[count - 1];
            list.RemoveAt(count - 1);

            return item;
        }

        public void Unshift(T item) => list.Insert(0, item);

        public void Push(T item) => list.Add(item);
    }
}
