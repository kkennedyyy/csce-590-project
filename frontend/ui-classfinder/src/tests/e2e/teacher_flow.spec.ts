import { expect, test } from '@playwright/test';

test.describe('teacher flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      window.localStorage.setItem(
        'classfinder.auth.v1',
        JSON.stringify({
          userId: 'teacher-2',
          role: 'teacher',
          name: 'Dr. Brown',
          email: 'brown@email.com',
        }),
      );
    });
  });

  test('teacher can remove a student and save class changes persistently', async ({ page }) => {
    await page.goto('/teachers');

    await expect(page.getByLabel('Class title')).toHaveValue('Software Engineering');
    await expect(page.getByText(/teacher workspace/i)).toBeVisible();

    await page.getByLabel('Class title').fill('Software Engineering Studio');
    await page.getByLabel('Location').fill('ZACH 210');
    await page.getByLabel('Capacity').fill('31');
    await page.getByRole('button', { name: /^remove$/i }).first().click();
    await page.getByRole('button', { name: /save changes/i }).click();

    await expect(page.getByLabel('Class title')).toHaveValue('Software Engineering Studio');
    await expect(page.getByLabel('Location')).toHaveValue('ZACH 210');
    await expect(page.locator('body')).toContainText('29/31 enrolled');

    await page.reload();

    await expect(page.getByLabel('Class title')).toHaveValue('Software Engineering Studio');
    await expect(page.getByLabel('Location')).toHaveValue('ZACH 210');
    await expect(page.locator('body')).toContainText('29/31 enrolled');
  });
});
