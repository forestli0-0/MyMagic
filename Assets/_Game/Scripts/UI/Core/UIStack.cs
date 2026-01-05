using System.Collections.Generic;

namespace CombatSystem.UI
{
    public sealed class UIStack<T> where T : class
    {
        private readonly List<T> items;

        public UIStack(int capacity = 4)
        {
            items = new List<T>(capacity);
        }

        public int Count => items.Count;

        public T Peek()
        {
            return items.Count > 0 ? items[items.Count - 1] : null;
        }

        public void Clear()
        {
            items.Clear();
        }

        public void Push(T item)
        {
            if (item != null)
            {
                items.Add(item);
            }
        }

        public T Pop()
        {
            if (items.Count == 0)
            {
                return null;
            }

            var index = items.Count - 1;
            var item = items[index];
            items.RemoveAt(index);
            return item;
        }

        public bool Remove(T item)
        {
            if (item == null)
            {
                return false;
            }

            for (var i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] == item)
                {
                    items.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool Contains(T item)
        {
            return items.Contains(item);
        }
    }
}
