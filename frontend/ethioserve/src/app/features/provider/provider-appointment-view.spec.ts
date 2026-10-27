import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppointmentStatus, ProviderAppointment } from '../../core/models/appointment.models';
import { ProviderAppointmentView } from './provider-appointment-view';

const inHours = (hours: number): string => new Date(Date.now() + hours * 3_600_000).toISOString();

function appointment(status: AppointmentStatus, startInHours: number): ProviderAppointment {
  return {
    id: 'a1',
    requestId: 'r1',
    requestTitle: 'Leaking kitchen pipe',
    serviceName: 'Pipe repair',
    cityName: 'Addis Ababa',
    startAt: inHours(startInHours),
    endAt: inHours(startInHours + 2),
    address: 'Bole, near the main road',
    notes: null,
    status,
    customerName: 'Test User',
    customerPhone: '0911223344',
    amount: 500,
    createdAt: inHours(-1),
  };
}

describe('ProviderAppointmentView', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function render(value: ProviderAppointment): Promise<ComponentFixture<ProviderAppointmentView>> {
    const fixture = TestBed.createComponent(ProviderAppointmentView);
    fixture.componentRef.setInput('appointment', value);
    await fixture.whenStable();

    return fixture;
  }

  const element = (fixture: ComponentFixture<ProviderAppointmentView>): HTMLElement => fixture.nativeElement as HTMLElement;

  const buttons = (fixture: ComponentFixture<ProviderAppointmentView>): string[] =>
    Array.from(element(fixture).querySelectorAll('button')).map((b) => b.textContent?.trim() ?? '');

  it('shows the customer contact details and the agreed price', async () => {
    const fixture = await render(appointment('Scheduled', 30));

    expect(element(fixture).textContent).toContain('Test User');
    expect(element(fixture).textContent).toContain('Bole, near the main road');
    expect(element(fixture).querySelector('a[href="tel:0911223344"]')).not.toBeNull();
    expect(element(fixture).textContent).toContain('500');
  });

  it('offers only changing and cancelling while the customer has not confirmed', async () => {
    const fixture = await render(appointment('Scheduled', 30));

    expect(buttons(fixture)).toEqual(['Change the time', 'Cancel appointment']);
  });

  it('keeps Start back until two hours before a confirmed appointment, and says when it opens', async () => {
    const fixture = await render(appointment('Confirmed', 48));

    expect(buttons(fixture)).toEqual(['Change the time', 'Cancel appointment']);
    expect(element(fixture).textContent).toContain('You can start the job from');
  });

  it('offers Start inside the two-hour window', async () => {
    const fixture = await render(appointment('Confirmed', 1.5));

    expect(buttons(fixture)).toEqual(['Start the job', 'Change the time', 'Cancel appointment']);
    expect(element(fixture).textContent).not.toContain('You can start the job from');
  });

  it('offers only completing a job that is under way', async () => {
    const fixture = await render(appointment('InProgress', -0.5));

    expect(buttons(fixture)).toEqual(['Mark as complete']);
  });

  it('offers nothing once the appointment is over', async () => {
    expect(buttons(await render(appointment('Completed', -5)))).toEqual([]);
    expect(buttons(await render(appointment('Cancelled', 30)))).toEqual([]);
  });

  it('asks before cancelling, then cancels and reports the new state', async () => {
    const fixture = await render(appointment('Scheduled', 30));
    const updates: ProviderAppointment[] = [];
    fixture.componentInstance.updated.subscribe((value) => updates.push(value));

    Array.from(element(fixture).querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Cancel appointment')?.click();
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain('Cancel this appointment?');
    httpMock.expectNone('/api/providers/me/appointments/a1/cancel');

    Array.from(element(fixture).querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Yes, cancel it')?.click();

    const post = httpMock.expectOne('/api/providers/me/appointments/a1/cancel');
    expect(post.request.method).toBe('POST');
    post.flush(appointment('Cancelled', 30));
    await fixture.whenStable();

    expect(updates).toHaveLength(1);
    expect(updates[0].status).toBe('Cancelled');
  });

  it('shows the reason when a step is refused', async () => {
    const fixture = await render(appointment('Confirmed', 1.5));

    Array.from(element(fixture).querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Start the job')?.click();
    httpMock
      .expectOne('/api/providers/me/appointments/a1/start')
      .flush({ title: 'Business rule violation', detail: 'An appointment that is Scheduled cannot be started.' }, { status: 422, statusText: 'Unprocessable' });
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain('cannot be started');
  });
});