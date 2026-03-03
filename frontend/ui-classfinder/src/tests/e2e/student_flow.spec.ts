import { expect, test } from '@playwright/test';

test.describe('student flow', () => {
  test('can add and remove classes, block full and overlap scenarios', async ({ page }) => {
    await page.goto('/browse');

    const browseSearch = page.getByRole('combobox', { name: 'browse' });
    await browseSearch.fill('CSCE101-01');
    await browseSearch.blur();
    await expect(page.locator('#browse-search-list')).toBeHidden();
    await page.getByRole('button', { name: /add csce101-01 to schedule/i }).click();
    await expect(
      page.locator('header').getByText('Current credits: 3 / 19', { exact: true }),
    ).toBeVisible();

    await page.getByRole('link', { name: /^Schedule$/ }).click();
    await expect(
      page.locator('header').getByText('Current credits: 3 / 19', { exact: true }),
    ).toBeVisible();

    await page.getByRole('link', { name: 'Browse' }).click();
    await browseSearch.fill('PHYS201');
    await browseSearch.blur();
    await expect(page.getByRole('button', { name: /add phys201-01 to schedule/i })).toBeDisabled();

    await browseSearch.fill('MATH200');
    await browseSearch.blur();
    await page.getByRole('button', { name: /add math200-02 to schedule/i }).click();
    await page.getByRole('link', { name: /^Schedule$/ }).click();

    await expect(page.getByTestId('conflict-overlay')).toHaveCount(2);
    await expect(page.getByRole('button', { name: /finalize registration/i })).toBeDisabled();

    await page.evaluate(() => {
      const removeButton = document.querySelector(
        'button[aria-label="Remove CSCE101-01"]',
      ) as HTMLButtonElement | null;
      removeButton?.click();
    });
    await expect(page.getByRole('button', { name: /finalize registration/i })).toBeEnabled();
  });

  test('enforces credit limit with actionable message', async ({ page }) => {
    await page.goto('/browse');

    const classes = [
      'CSCE101-01',
      'CSCE210-01',
      'CSCE312-01',
      'CSCE313-01',
      'HIST230-01',
      'CHEM107-01',
      'BIOL111-01',
    ];

    for (const classId of classes) {
      const browseSearch = page.getByRole('combobox', { name: 'browse' });
      await browseSearch.fill(classId);
      await browseSearch.blur();
      const add = page.getByRole('button', {
        name: new RegExp(`add ${classId.toLowerCase()} to schedule`, 'i'),
      });
      await add.click();
    }

    await expect(page.getByRole('alert').filter({ hasText: /exceed 19 credits|contact advisor/i })).toBeVisible();
  });
});
