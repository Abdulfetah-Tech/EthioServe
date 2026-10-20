import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Pager } from './pager';

describe('Pager', () => {
  async function render(page: number, totalPages: number): Promise<ComponentFixture<Pager>> {
    const fixture = TestBed.createComponent(Pager);
    fixture.componentRef.setInput('page', page);
    fixture.componentRef.setInput('totalPages', totalPages);
    await fixture.whenStable();

    return fixture;
  }

  const buttons = (fixture: ComponentFixture<Pager>): HTMLButtonElement[] =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));

  it('shows nothing when there is only one page', async () => {
    const fixture = await render(1, 1);

    expect(buttons(fixture)).toHaveLength(0);
  });

  it('disables Previous on the first page and Next on the last', async () => {
    const first = await render(1, 3);
    expect(buttons(first)[0].disabled).toBe(true);
    expect(buttons(first)[1].disabled).toBe(false);

    const last = await render(3, 3);
    expect(buttons(last)[0].disabled).toBe(false);
    expect(buttons(last)[1].disabled).toBe(true);
  });

  it('shows the current position', async () => {
    const fixture = await render(2, 5);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Page 2 of 5');
  });

  it('emits the neighbouring page numbers', async () => {
    const fixture = await render(2, 5);
    const pages: number[] = [];
    fixture.componentInstance.pageChange.subscribe((page) => pages.push(page));

    buttons(fixture)[0].click();
    buttons(fixture)[1].click();

    expect(pages).toEqual([1, 3]);
  });
});