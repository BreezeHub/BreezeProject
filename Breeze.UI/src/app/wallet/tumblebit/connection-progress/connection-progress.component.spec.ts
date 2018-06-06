import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { ConnectionProgressComponent } from './connection-progress.component';

describe('ConnectionProgressComponent', () => {
  let component: ConnectionProgressComponent;
  let fixture: ComponentFixture<ConnectionProgressComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ ConnectionProgressComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ConnectionProgressComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
