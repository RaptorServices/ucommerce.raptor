# Ucommerce.Raptor
Umbraco package for handling the relationship between Ucommerce and Raptor

This package contains a Umbraco web api end point, which is responsible for making product data accessible for Raptor and a umbraco macro, which serves as an example of how to implement a Raptor recommendation module.

The macro can be added to a template in umbraco, and then it will display the raw JSON from the Raptor module.
The rendering of the recommendations can be done by the developer, based on the JSON returned from the raptor module.

All available modules, as well as the customerid and the api key can be obtained by logging in to the Raptor Controlpanel, https://controlpanel.raptorsmartadvisor.com.

## Building the Umbraco Package
The only thing you need to do in order to build a package which you can install directly into the backoffice of Umbraco, is to build this project in release mode.

When that is done the package to be installed is located at slnPath/PublishOutput named "Ucommerce-Raptor.zip".
