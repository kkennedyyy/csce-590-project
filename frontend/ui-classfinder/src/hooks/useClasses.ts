import { useCallback, useEffect, useMemo, useState } from 'react';

import { fetchClasses } from '../api/api';
import type { ClassOffering } from '../types';
import { fuzzySearchClasses, type SearchResult } from '../utils/search';

const PAGE_SIZE = 8;

export function useClasses(search: string): {
  classes: ClassOffering[];
  filtered: SearchResult[];
  hasMore: boolean;
  loading: boolean;
  error: string | null;
  loadMore: () => Promise<void>;
  refresh: () => Promise<void>;
} {
  const [classes, setClasses] = useState<ClassOffering[]>([]);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setLoading(true);
      const response = await fetchClasses({ page: 1, pageSize: PAGE_SIZE, search: '' });
      setClasses(response.classes);
      setPage(1);
      setHasMore(response.hasMore);
      setError(null);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch classes.';
      setError(message);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadMore = useCallback(async () => {
    if (loading || !hasMore) {
      return;
    }

    try {
      setLoading(true);
      const nextPage = page + 1;
      const response = await fetchClasses({ page: nextPage, pageSize: PAGE_SIZE, search: '' });
      setClasses((prev) => [...prev, ...response.classes]);
      setPage(nextPage);
      setHasMore(response.hasMore);
      setError(null);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to load classes.';
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [hasMore, loading, page]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const filtered = useMemo(() => fuzzySearchClasses(classes, search), [classes, search]);

  return {
    classes,
    filtered,
    hasMore,
    loading,
    error,
    loadMore,
    refresh,
  };
}
