import Fuse, { type FuseResult, type IFuseOptions } from 'fuse.js';

import type { ClassOffering } from '../types';

export interface SearchResult {
  item: ClassOffering;
  badges: string[];
}

const fuseOptions: IFuseOptions<ClassOffering> = {
  keys: ['id', 'title', 'instructor'],
  threshold: 0.35,
  ignoreLocation: true,
  minMatchCharLength: 2,
  includeMatches: true,
};

export function fuzzySearchClasses(classes: ClassOffering[], query: string): SearchResult[] {
  const term = query.trim();

  if (!term) {
    return classes.map((item) => ({ item, badges: [] }));
  }

  const normalized = term.toLowerCase();
  const direct = classes
    .filter(
      (item) =>
        item.id.toLowerCase().includes(normalized) ||
        item.title.toLowerCase().includes(normalized) ||
        item.instructor.toLowerCase().includes(normalized),
    )
    .map((item) => ({
      item,
      badges: collectDirectBadges(item, normalized),
    }));

  const fuse = new Fuse(classes, fuseOptions);
  const fuzzy = fuse.search(term).map((result) => ({
    item: result.item,
    badges: collectFuseBadges(result),
  }));

  const deduped = new Map<string, SearchResult>();
  [...direct, ...fuzzy].forEach((entry) => {
    if (!deduped.has(entry.item.id)) {
      deduped.set(entry.item.id, entry);
    }
  });

  return Array.from(deduped.values());
}

function collectDirectBadges(item: ClassOffering, term: string): string[] {
  const badges: string[] = [];

  if (item.id.toLowerCase().includes(term)) {
    badges.push('ID');
  }
  if (item.title.toLowerCase().includes(term)) {
    badges.push('Title');
  }
  if (item.instructor.toLowerCase().includes(term)) {
    badges.push('Instructor');
  }

  return badges;
}

function collectFuseBadges(result: FuseResult<ClassOffering>): string[] {
  const badges = new Set<string>();

  result.matches?.forEach((match) => {
    if (match.key === 'id') {
      badges.add('ID');
    }
    if (match.key === 'title') {
      badges.add('Title');
    }
    if (match.key === 'instructor') {
      badges.add('Instructor');
    }
  });

  return Array.from(badges);
}
