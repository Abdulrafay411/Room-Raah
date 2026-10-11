import { TestBed } from '@angular/core/testing';
import { MailpitTipComponent, mailpitUrl } from './mailpit-tip.component';

describe('MailpitTipComponent', () => {
  it('opens Mailpit on the API host, in a new tab', () => {
    const fixture = TestBed.createComponent(MailpitTipComponent);
    fixture.detectChanges();
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('[data-smoke="open-mailpit"]');

    expect(link.getAttribute('href')).toBe(fixture.componentInstance.url);
    expect(link.target).toBe('_blank');
    expect(link.rel).toContain('noopener');
  });
});

describe('mailpitUrl', () => {
  it('keeps the API host and swaps in the Mailpit port', () => {
    expect(mailpitUrl('http://localhost:5019/api/v1')).toBe('http://localhost:8025');
    expect(mailpitUrl('http://10.0.0.5:8080/api/v1')).toBe('http://10.0.0.5:8025');
  });
});
