This is a simple app that scans a folder recursively looking for any instances
of a dll with a particular name. If it finds that dll, it extracts the product
version and file versions from it and writes a output row in CSV format with
those fields.

It also looks within Zip files for instances of the dll, and if it finds one
it'll extract the dll and read the product and file versions and also write
those into an output row in CSV.
