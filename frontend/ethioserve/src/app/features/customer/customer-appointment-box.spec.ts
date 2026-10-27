import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppointmentStatus, CustomerAppointment } from '../../core/models/appointment.models';
import { RequestStatus } from '../../core/models/request.models';
import { CustomerAppointmentBox } from './customer-appointment-box';

const inHours = (hours: number): string => new Date(Date.now() + hours * 3_600_000).toISOString();

function appointment(status: AppointmentStatus): CustomerAppointment {
  return {
    id: 'a1',
    requestId: 'r1',
    requestTitle: 'Leaking kitchen pipe',
    serviceName: 'Pipe repair',
    startAt: inHours(30),
    endAt: inHours(32),
    address: 'Bole, near the main road',
    notes: 'Bring spare pipes',
    status,
    providerId: 'p1',
    providerName: 'Alpha Plumbing',
    providerPhone: '0911556677',
    amount: 500,
    createdAt: inHours(-1),
  };
}

const page = (items: CustomerAppointment[]) => ({ items, page: 1, pageSize: 5, totalCount: items.length, totalPages: 1 });

describe('CustomerAppointmentBox', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // Both lists are asked for at once: the upcoming ones and the history.
  function answer(upcoming: CustomerAppointment[], history: CustomerAppointment[]): void {
    const calls = httpMock.match((r) => r.url === '/api/appointments');

    expect(calls).toHaveLength(2);

    for (const call of calls) {
      call.flush(page(call.request.params.get('scope') === 'History' ? history : upcoming));
    }
  }

  async function render(
    upcoming: CustomerAppointment[],
    history: CustomerAppointment[] = [],
    status: RequestStatus = 'Scheduled',
  ): Promise<ComponentFixture<CustomerAppointmentBox>> {
    const fixture = TestBed.createComponent(CustomerAppointmentBox);
    fixture.componentRef.setInput('requestId', 'r1');
    fixture.componentRef.setInput('requestStatus', status);
    await fixture.whenStable();

    answer(upcoming, history);
    await fixture.whenStable();

    return fixture;
  }

  const element = (fixture: ComponentFixture<CustomerAppointmentBox>): HTMLElement => fixture.nativeElement as HTMLElement;

  const button = (fixture: ComponentFixture<CustomerAppointmentBox>, text: string): HTMLButtonElement | undefined =>
    Array.from(element(fixture).querySelectorAll('button')).find((b) => b.textContent?.trim() === text);

  it('shows the proposed time, the provider contact details and asks the customer to confirm', async () => {
    const fixture = await render([appointment('Scheduled')]);

    expect(element(fixture).textContent).toContain('Please confirm this time');
    expect(element(fixture).textContent).toContain('Alpha Plumbing');
    expect(element(fixture).textContent).toContain('Bring spare pipes');
    expect(element(fixture).querySelector('a[href="tel:0911556677"]')).not.toBeNull();
    expect(button(fixture, 'Confirm this time')).toBeDefined();
    expect(button(fixture, 'Cancel appointment')).toBeDefined();
  });

  it('offers only cancelling once the time is confirmed', async () => {
    const fixture = await render([appointment('Confirmed')]);

    expect(button(fixture, 'Confirm this time')).toBeUndefined();
    expect(button(fixture, 'Cancel appointment')).toBeDefined();
    expect(element(fixture).textContent).not.toContain('Please confirm this time');
  });

  it('confirms with one click, tells the page, and reloads', async () => {
    const fixture = await render([appointment('Scheduled')]);
    let changed = 0;
    fixture.componentInstance.changed.subscribe(() => changed++);

    button(fixture, 'Confirm this time')?.click();

    const post = httpMock.expectOne('/api/appointments/a1/confirm');
    expect(post.request.method).toBe('POST');
    post.flush(appointment('Confirmed'));
    await fixture.whenStable();

    answer([appointment('Confirmed')], []);
    await fixture.whenStable();

    expect(changed).toBe(1);
    expect(element(fixture).textContent).toContain('You confirmed this time');
    expect(button(fixture, 'Confirm this time')).toBeUndefined();
  });

  it('asks before cancelling and sends nothing if the customer keeps it', async () => {
    const fixture = await render([appointment('Confirmed')]);

    button(fixture, 'Cancel appointment')?.click();
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain('can then propose a new time');
    httpMock.expectNone('/api/appointments/a1/cancel');

    button(fixture, 'Keep it')?.click();
    await fixture.whenStable();

    expect(button(fixture, 'Yes, cancel it')).toBeUndefined();
    expect(button(fixture, 'Cancel appointment')).toBeDefined();
  });

  it('says who the customer is waiting for before a time is proposed', async () => {
    const fixture = await render([], [], 'Accepted');

    expect(element(fixture).textContent).toContain('Waiting for the provider to propose a time');
    expect(element(fixture).querySelectorAll('button')).toHaveLength(0);
  });

  it('says so after a cancellation', async () => {
    const fixture = await render([], [appointment('Cancelled')], 'Accepted');

    expect(element(fixture).textContent).toContain('The last appointment was cancelled');
  });

  it('shows a finished job without any buttons', async () => {
    const fixture = await render([], [appointment('Completed')], 'Completed');

    expect(element(fixture).textContent).toContain('Completed');
    expect(element(fixture).querySelectorAll('button')).toHaveLength(0);
  });
});