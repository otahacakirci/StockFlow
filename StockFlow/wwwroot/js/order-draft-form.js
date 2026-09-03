(function () {
    "use strict";

    const formHost = document.querySelector("[data-order-draft-form-host]");
    const form = document.querySelector("[data-order-draft-form]");
    if (!formHost || !form) {
        return;
    }

    const typeSelect = form.querySelector("[data-order-type]");
    const saleParty = form.querySelector("[data-sale-party]");
    const purchaseParty = form.querySelector("[data-purchase-party]");
    const customerSelect = form.querySelector("[data-customer-select]");
    const supplierSelect = form.querySelector("[data-supplier-select]");
    const itemList = form.querySelector("[data-order-item-list]");
    const addItemButton = form.querySelector("[data-add-order-item]");

    function updatePartyFields() {
        const isSale = typeSelect.value === "1";
        saleParty.classList.toggle("d-none", !isSale);
        saleParty.setAttribute("aria-hidden", String(!isSale));
        purchaseParty.classList.toggle("d-none", isSale);
        purchaseParty.setAttribute("aria-hidden", String(isSale));
        customerSelect.disabled = !isSale;
        supplierSelect.disabled = isSale;
    }

    function replaceIndex(value, index) {
        return value
            .replace(/Input\.Items\[\d+\]/g, `Input.Items[${index}]`)
            .replace(/Input_Items_\d+__/g, `Input_Items_${index}__`);
    }

    function reindexRows() {
        const rows = Array.from(itemList.querySelectorAll("[data-order-item-row]"));
        rows.forEach((row, index) => {
            row.querySelector("[data-item-number]").textContent = String(index + 1);
            row.querySelectorAll("[name], [id], [for], [data-valmsg-for]").forEach(element => {
                ["name", "id", "for", "data-valmsg-for"].forEach(attribute => {
                    const value = element.getAttribute(attribute);
                    if (value) {
                        element.setAttribute(attribute, replaceIndex(value, index));
                    }
                });
            });
        });

        rows.forEach(row => {
            row.querySelector("[data-remove-order-item]").disabled = rows.length === 1;
        });
    }

    function resetValidation() {
        if (!window.jQuery || !window.jQuery.validator || !window.jQuery.validator.unobtrusive) {
            return;
        }

        const jqueryForm = window.jQuery(formHost);
        jqueryForm.removeData("validator");
        jqueryForm.removeData("unobtrusiveValidation");
        window.jQuery.validator.unobtrusive.parse(jqueryForm);
    }

    function addItem() {
        const sourceRow = itemList.querySelector("[data-order-item-row]");
        if (!sourceRow) {
            return;
        }

        const newRow = sourceRow.cloneNode(true);
        newRow.querySelector("[data-product-select]").value = "";
        newRow.querySelector("input").value = "1";
        newRow.querySelectorAll("[data-valmsg-for]").forEach(message => {
            message.textContent = "";
            message.classList.remove("field-validation-error");
            message.classList.add("field-validation-valid");
        });
        itemList.appendChild(newRow);
        reindexRows();
        resetValidation();
        newRow.querySelector("[data-product-select]").focus();
    }

    itemList.addEventListener("click", event => {
        const removeButton = event.target.closest("[data-remove-order-item]");
        if (!removeButton) {
            return;
        }

        const rows = itemList.querySelectorAll("[data-order-item-row]");
        if (rows.length <= 1) {
            return;
        }

        removeButton.closest("[data-order-item-row]").remove();
        reindexRows();
        resetValidation();
    });

    typeSelect.addEventListener("change", updatePartyFields);
    addItemButton.addEventListener("click", addItem);
    updatePartyFields();
    reindexRows();
}());
