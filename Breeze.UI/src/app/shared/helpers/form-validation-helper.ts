import { FormGroup } from "@angular/forms";

export const formValidator = (form: FormGroup, errors: any, validationMessages: any) => {
    if (!form) { return; }
    for (const field in errors) {
        if (!errors.hasOwnProperty(field)) {
            continue;
        }
        errors[field] = '';
        const control = form.get(field);
        if (control && control.dirty && !control.valid) {
            const messages = validationMessages[field];
            for (const key in control.errors) {
                if (control.errors.hasOwnProperty(key)) {
                    errors[field] += messages[key] + ' ';
                }
            }
        }
    }
}