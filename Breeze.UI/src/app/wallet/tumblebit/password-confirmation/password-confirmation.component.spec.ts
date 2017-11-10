import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { PasswordConfirmationComponent } from './password-confirmation.component';

describe('PasswordConfirmationComponent', () => {
  let component: PasswordConfirmationComponent;
  let fixture: ComponentFixture<PasswordConfirmationComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ PasswordConfirmationComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(PasswordConfirmationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should be created', () => {
    expect(component).toBeTruthy();
  });
});
