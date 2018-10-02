import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FormBuilder } from '@angular/forms';
import 'rxjs/add/operator/filter';
import 'rxjs/add/operator/takeUntil';

import { ApiService } from '../../shared/services/api.service';
import { GlobalService } from '../../shared/services/global.service';
import { TumblebitService } from './tumblebit.service';
import { ModalService } from '../../shared/services/modal.service';

import { TumblebitComponent } from './tumblebit.component';

@Component({
    selector: 'stratis-tumblebit-component',
    providers: [TumblebitService],
    templateUrl: './tumblebit.component.html',
    styleUrls: ['./tumblebit.component.css'],
})
export class StratisTumblebitComponent extends TumblebitComponent {

    constructor(apiService: ApiService,
                tumblebitService: TumblebitService,
                globalService: GlobalService,
                modalService: NgbModal,
                genericModalService: ModalService,
                fb: FormBuilder,
                router: Router) {
        super(apiService, tumblebitService, globalService, modalService, genericModalService, fb, router);

        this.isBitcoin = false;
    }
}