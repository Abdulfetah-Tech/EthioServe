import { TestBed } from '@angular/core/testing';
import { Rating } from './rating';

describe('Rating', () => {
  async function render(value: number, count: number): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(Rating);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('count', count);
    await fixture.whenStable();

    return fixture.nativeElement as HTMLElement;
  }

  it('shows the rating with one decimal and the review count', async () => {
    const element = await render(4.9, 88);

    expect(element.textContent).toContain('4.9');
    expect(element.textContent).toContain('(88)');
  });

  it('describes the rating in words for screen readers', async () => {
    const element = await render(4.9, 88);

    expect(element.querySelector('[role="img"]')?.getAttribute('aria-label')).toBe('Rated 4.9 out of 5 from 88 reviews');
  });

  it('says so when there are no reviews yet', async () => {
    const element = await render(0, 0);

    expect(element.textContent).toContain('No reviews yet');
    expect(element.querySelector('[role="img"]')).toBeNull();
  });
});