import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { ConnectionModalComponent } from './connection-modal.component';

describe('ConnectionModalComponent', () => {
  let component: ConnectionModalComponent;
  let fixture: ComponentFixture<ConnectionModalComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ ConnectionModalComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ConnectionModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
