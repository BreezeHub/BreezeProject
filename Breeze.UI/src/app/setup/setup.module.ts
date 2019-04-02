import { NgModule } from '@angular/core';
import { BsDatepickerModule } from 'ngx-bootstrap/datepicker';

import { SetupComponent } from './setup.component';
import { CreateComponent } from './create/create.component';

import { SetupRoutingModule } from './setup-routing.module';
import { RecoverComponent } from './recover/recover.component';
import { ShowMnemonicComponent } from './create/show-mnemonic/show-mnemonic.component';
import { ConfirmMnemonicComponent } from './create/confirm-mnemonic/confirm-mnemonic.component';
import { SharedModule } from '../shared/shared.module';

@NgModule({
  imports: [
    BsDatepickerModule.forRoot(),
    SharedModule,
    SetupRoutingModule
  ],
  declarations: [
    CreateComponent,
    SetupComponent,
    RecoverComponent,
    ShowMnemonicComponent,
    ConfirmMnemonicComponent
  ],
  exports: [],
  providers: []
})

export class SetupModule { }
