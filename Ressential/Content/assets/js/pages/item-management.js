
        $(function () {
            $('[data-toggle="tooltip"]').tooltip();
        });
        $(document).ready(function () {
            // Function to add a new row
            $("#addRowBtn").click(function () {
                var newRow;
                if (document.querySelector('.items-table-price')) {
                    var newRow = `
                    <tr>
                        <td class="tbl-min-width-6 col-3 mb-3 mt-3">
                            <div class="form-floating">
                                <select class="form-control" name="unitID[]">
                                    <option selected value="0">Select Item</option>
                                    <option value="1">Sugar</option>
                                    <option value="2">Chicken</option>
                                    <option value="3">Beef</option>
                                    <option value="4">Fries</option>
                                </select>
                                <label>Item</label>
                            </div>
                        </td>
                        <td class="tbl-min-width-6 col-3 mb-3 mt-3">
                            <div class="form-floating">
                                <input type="text" class="form-control" placeholder="Description" name="description[]">
                                <label>Description</label>
                            </div>
                        </td>
                        <td class="tbl-min-width-4 col-2 mb-3 mt-3">
                            <div class="form-floating">
                                <input type="number" class="form-control quantity" placeholder="Quantity" name="quantity[]">
                                <label>Quantity</label>
                            </div>
                        </td>
                        <td class="tbl-min-width-4 col-2 mb-3 mt-3">
                            <div class="form-floating">
                                <input type="number" class="form-control unit-price" placeholder="Unit Price" name="unitPrice[]">
                                <label>Unit Price</label>
                            </div>
                        </td>
                        <td class="tbl-min-width-5 col-2 mb-3 mt-3">
                            <div class="form-floating">
                                <input type="number" class="form-control total" placeholder="Total" name="total[]">
                                <label>Total</label>
                            </div>
                        </td>
                         <td class="tbl-min-width-1">
                                            <button class="btn btn-danger btn-sm rounded-2 m-2 removeRowBtn"
                                                    type="button" data-toggle="tooltip" data-placement="top"
                                                    title="Remove">
                                                <i class="fa fa-times"></i>
                                            </button>
                                        </td>
                    </tr>`;
                } else if (document.querySelector('.items-table-without-price')) {
                    var newRow = `
                    <tr>
                        <td class="tbl-min-width-6 col-3 mb-3 mt-3">
                            <div class="form-floating">
                                <select class="form-control" name="unitID[]">
                                    <option selected value="0">Select Item</option>
                                    <option value="1">Sugar</option>
                                    <option value="2">Chicken</option>
                                    <option value="3">Beef</option>
                                    <option value="4">Fries</option>
                                </select>
                                <label>Item</label>
                            </div>
                        </td>
                        <td class="tbl-min-width-6 col-3 mb-3 mt-3">
                            <div class="form-floating">
                                <input type="text" class="form-control" placeholder="Description" name="description[]">
                                <label>Description</label>
                            </div>
                        </td>
                        <td class="tbl-min-width-4 col-2 mb-3 mt-3">
                            <div class="form-floating">
                                <input type="number" class="form-control quantity" placeholder="Quantity" name="quantity[]">
                                <label>Quantity</label>
                            </div>
                        </td>
                         <td class="tbl-min-width-1">
                                            <button class="btn btn-danger btn-sm rounded-2 m-2 removeRowBtn"
                                                    type="button" data-toggle="tooltip" data-placement="top"
                                                    title="Remove">
                                                <i class="fa fa-times"></i>
                                            </button>
                                        </td>
                    </tr>`;
                }
                else if (document.querySelector('.items-table-without-description')) {
                    var newRow = `
                    <tr>
                        <td class="tbl-min-width-7 col-3 mb-3 mt-3">
                            <div class="form-floating">
                                <select class="form-control" name="unitID[]">
                                    <option selected value="0">Select Item</option>
                                    <option value="1">Sugar</option>
                                    <option value="2">Chicken</option>
                                    <option value="3">Beef</option>
                                    <option value="4">Fries</option>
                                </select>
                                <label>Item</label>
                            </div>
                        </td>
                        <td class="tbl-min-width-4 col-2 mb-3 mt-3">
                            <div class="form-floating">
                                <input type="number" class="form-control quantity" placeholder="Quantity" name="quantity[]">
                                <label>Quantity</label>
                            </div>
                        </td>
                        <td class="tbl-min-width-4 col-2 mb-3 mt-3">
                            <div class="form-floating">
                                <input type="number" class="form-control unit-price" placeholder="Unit Price" name="unitPrice[]" readonly>
                                <label>Unit Price</label>
                            </div>
                        </td>
                        <td class="tbl-min-width-5 col-2 mb-3 mt-3">
                            <div class="form-floating">
                                <input type="number" class="form-control total" placeholder="Total" name="total[]" readonly>
                                <label>Total</label>
                            </div>
                        </td>
                         <td class="tbl-min-width-1">
                                            <button class="btn btn-danger btn-sm rounded-2 m-2 removeRowBtn"
                                                    type="button" data-toggle="tooltip" data-placement="top"
                                                    title="Remove">
                                                <i class="fa fa-times"></i>
                                            </button>
                                        </td>
                    </tr>`;
                }
                
                $("#itemsTable tbody").append(newRow);
                $('[data-toggle="tooltip"]').tooltip();
            });

            // Function to remove a row
            $(document).on('click', '.removeRowBtn', function () {
                $(this).tooltip('dispose');
                $(this).closest('tr').remove();
                calculateGrandTotal();
            });

            // Function to calculate unit price or total based on user input
            $(document).on('input', '.quantity, .unit-price, .total', function () {
                var row = $(this).closest('tr');
                var quantity = parseFloat(row.find('.quantity').val()) || 0;
                var unitPrice = parseFloat(row.find('.unit-price').val()) || 0;
                var total = parseFloat(row.find('.total').val()) || 0;

                if ($(this).hasClass('unit-price')) {
                    // Calculate total based on quantity and unit price
                    row.find('.total').val((quantity * unitPrice).toFixed(2));
                } else if ($(this).hasClass('total')) {
                    // Calculate unit price based on quantity and total
                    if (quantity !== 0) {
                        row.find('.unit-price').val((total / quantity).toFixed(2));
                    }
                } else if ($(this).hasClass('quantity')) {
                    // If quantity changes, update the total if unit price is available
                    if (unitPrice !== 0) {
                        row.find('.total').val((quantity * unitPrice).toFixed(2));
                    } else if (total !== 0) {
                        row.find('.unit-price').val((total / quantity).toFixed(2));
                    }
                }

                calculateGrandTotal();
            });

            // Function to calculate grand total
            function calculateGrandTotal() {
                var grandTotal = 0;
                $('#itemsTable tbody tr').each(function () {
                    var total = parseFloat($(this).find('.total').val()) || 0;
                    grandTotal += total;
                });

                document.getElementById('totalAmount').value = grandTotal.toFixed(2);
                // Update a grand total field if needed, e.g., $('#grandTotal').val(grandTotal.toFixed(2));
            }
        });

