import {
  useCallback,
  useEffect,
  useState,
  type Dispatch,
  type SetStateAction,
} from "react";

const sessionValues = new Map<string, unknown>();

function valueForKey<T>(key: string, fallback: T): T {
  return sessionValues.has(key) ? (sessionValues.get(key) as T) : fallback;
}

export function useSessionStoredState<T>(
  key: string,
  fallback: T,
): [T, Dispatch<SetStateAction<T>>] {
  const [value, setValue] = useState(() => valueForKey(key, fallback));

  useEffect(() => {
    setValue(valueForKey(key, fallback));
  }, [key]);

  const setStoredValue = useCallback<Dispatch<SetStateAction<T>>>(
    (action) => {
      setValue((current) => {
        const next =
          typeof action === "function"
            ? (action as (current: T) => T)(current)
            : action;
        sessionValues.set(key, next);
        return next;
      });
    },
    [key],
  );

  return [value, setStoredValue];
}
