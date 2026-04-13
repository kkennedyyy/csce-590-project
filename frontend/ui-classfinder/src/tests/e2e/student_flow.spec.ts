import { expect, test } from '@playwright/test';

test.describe('student flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      window.localStorage.setItem(
        'classfinder.auth.v1',
        JSON.stringify({
          userId: 'student-123',
          role: 'student',
          name: 'John Smith',
          email: 'john.smith@email.com',
        }),
      );
    });
  });

  test('can enroll and drop classes while blocking full and overlapping choices', async ({ page }) => {
    await page.goto('/browse');

    const browseSearch = page.getByRole('combobox', { name: 'browse' });
    await browseSearch.fill('CSCE101-01');
    await browseSearch.blur();
    await expect(page.locator('#browse-search-list')).toBeHidden();
    await page.getByRole('button', { name: /enroll csce101-01/i }).click();
    await page.waitForFunction(() =>
      (window.localStorage.getItem('classfinder.schedules.v2') ?? '').includes('CSCE101-01'),
    );

    await page.goto('/schedule');
    await expect(page.getByRole('status', { name: /current credits/i })).toContainText(
      'Current credits: 3 / 19',
    );

    await page.goto('/browse');
    await browseSearch.fill('PHYS201');
    await browseSearch.blur();
    await expect(page.getByRole('button', { name: /enroll phys201-01/i })).toBeDisabled();

    await browseSearch.fill('MATH200');
    await browseSearch.blur();
    await page.getByRole('button', { name: /enroll math200-02/i }).click();
    await expect(page.getByRole('alert').filter({ hasText: /overlap detected/i })).toBeVisible();

    await page.goto('/schedule');
    await expect(page.getByTestId('conflict-overlay')).toHaveCount(0);

    await page.evaluate(() => {
      const removeButton = document.querySelector(
        'button[aria-label="Remove CSCE101-01"]',
      ) as HTMLButtonElement | null;
      removeButton?.click();
    });

    await expect(page.getByRole('status', { name: /current credits/i })).toContainText(
      'Current credits: 0 / 19',
    );
  });

  test('shows prerequisite failures from the catalog', async ({ page }) => {
    await page.goto('/browse');

    const browseSearch = page.getByRole('combobox', { name: 'browse' });
    await browseSearch.fill('CSCE420');
    await browseSearch.blur();
    await page.getByRole('button', { name: /enroll csce420-01/i }).click();

    await expect(page.getByRole('alert').filter({ hasText: /missing prerequisites: csce331/i })).toBeVisible();
  });
});
