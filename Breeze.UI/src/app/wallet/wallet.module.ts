import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NgbModule } from '@ng-bootstrap/ng-bootstrap';
import { ClipboardModule } from 'ngx-clipboard';
import { SharedModule } from '../shared/shared.module';
import { WalletRoutingModule } from './wallet-routing.module';

import { WalletComponent } from './wallet.component';
import { MenuComponent } from './menu/menu.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { TumblebitComponent } from './tumblebit/tumblebit.component';
import { StratisTumblebitComponent } from './tumblebit/stratis-tumblebit.component';
import { HistoryComponent } from './history/history.component';
import { SidebarComponent } from './sidebar/sidebar.component';
import { StatusBarComponent } from './status-bar/status-bar.component';
import { ConnectionProgressComponent } from './tumblebit/connection-progress/connection-progress.component';

@NgModule({
  imports: [
    CommonModule,
    ClipboardModule,
    FormsModule,
    SharedModule.forRoot(),
    NgbModule,
    ReactiveFormsModule,
    WalletRoutingModule
  ],
  declarations: [
    WalletComponent,
    MenuComponent,
    DashboardComponent,
    TumblebitComponent,
    StratisTumblebitComponent,
    HistoryComponent,
    SidebarComponent,
    StatusBarComponent,
    ConnectionProgressComponent
  ],
  exports: []
})

export class WalletModule { }
