import { ClipboardModule } from 'ngx-clipboard';
import { CommonModule } from '@angular/common';
import { ConnectionProgressComponent } from './tumblebit/connection-progress/connection-progress.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HistoryComponent } from './history/history.component';
import { MenuComponent } from './menu/menu.component';
import { NgbModule } from '@ng-bootstrap/ng-bootstrap';
import { NgModule } from '@angular/core';
import { sharedComponents } from '../shared/components'
import { SharedModule } from '../shared/shared.module';
import { SidebarComponent } from './sidebar/sidebar.component';
import { StatusBarComponent } from './status-bar/status-bar.component';
import { TumblebitComponent } from './tumblebit/tumblebit.component';
import { WalletComponent } from './wallet.component';
import { WalletRoutingModule } from './wallet-routing.module';

@NgModule({
  imports: [
    ClipboardModule,
    CommonModule,
    FormsModule,
    NgbModule,
    ReactiveFormsModule,
    SharedModule.forRoot(),
    WalletRoutingModule
  ],
  declarations: [
    WalletComponent,
    MenuComponent,
    DashboardComponent,
    TumblebitComponent,
    HistoryComponent,
    SidebarComponent,
    StatusBarComponent,
    ConnectionProgressComponent,
    ...sharedComponents
  ],
  exports: []
})

export class WalletModule { }
