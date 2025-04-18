namespace RizzziGit.Commons.Utilities;

internal static class IEnumerableExtensions {
    extension<T>(IEnumerable<T> enumerable) {
        public IEnumerable<T> WhileEach(Func<T, bool> test, bool includeLast = false) {
            foreach (T item in enumerable) {
                if (!test(item)) {
                    if (includeLast) {
                        yield return item;
                    }

                    yield break;
                }

                yield return item;
            }
        }
    }

    extension<T>(IEnumerator<T> enumerator) {
        public IEnumerable<T> ToEnumerable() {
            while (enumerator.MoveNext()) {
                yield return enumerator.Current;
            }
        }
    }
}
