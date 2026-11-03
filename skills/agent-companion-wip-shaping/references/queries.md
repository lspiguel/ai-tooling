# FetchXML for shaping

Every query goes through the plugin's `query` tool: explicit columns only (`<all-attributes />` is refused), no `top` (use `pageSize`), and results are paged — continue with `nextCursor` until it is `null` when the check needs every row. All tables below are on the plugin's configuration allow-list, so none of these touch records. The plug-in registration tables in the research queries at the end joined the list in plugin version 1.2026.11.1.

GUIDs go in without braces. Table filters on `systemform` and `savedquery` use the table's **type code** (`objectTypeCode` from `list_tables`), not its logical name.

## Transport home and overlap

One query serves W2 (where a component lives) and W7 (who else holds it). Put the component's ID in the `in` list and, for a column, form, view or relationship, its table's `metadataId` as well.

```xml
<fetch>
  <entity name="solutioncomponent">
    <attribute name="objectid" />
    <attribute name="componenttype" />
    <attribute name="rootcomponentbehavior" />
    <attribute name="rootsolutioncomponentid" />
    <filter>
      <condition attribute="objectid" operator="in">
        <value>00000000-0000-0000-0000-000000000001</value>
        <value>00000000-0000-0000-0000-000000000002</value>
      </condition>
    </filter>
    <link-entity name="solution" from="solutionid" to="solutionid" alias="s">
      <attribute name="uniquename" />
      <attribute name="ismanaged" />
      <filter>
        <condition attribute="uniquename" operator="not-in">
          <value>Default</value>
          <value>Active</value>
        </condition>
      </filter>
    </link-entity>
  </entity>
</fetch>
```

Read each row's `s.uniquename` against the delivery context:

| The solution is | Means |
|---|---|
| A transport solution, holding the component itself | Its transport home (W2) |
| A transport solution, holding the component's **table** with `rootcomponentbehavior` 0 | Its transport home, through the table |
| A transport solution, holding the table with behaviour 1 or 2 only | Not a home for the child |
| Managed (`s.ismanaged` true) | An installed app or the system — not a home, not an overlap |
| This task's own WIP | Ignore |
| The environment's *Common Data Services Default Solution* — an unmanaged solution whose unique name varies (`Cr` and a few characters); find it once with `list_solutions` by its friendly name | Ignore. The maker portal puts every component made there into it, with behaviour 0; it is not a WIP |
| Any other unmanaged solution | An overlap to report (W7) — another WIP, whether agent-made (`_Agent_WIP_`) or made by hand. For a table, any behaviour counts: its ribbon diff travels with it (X6) |

Under strategy A, run the query on the build environment's tab for the home (T4), and on development for the overlap.

## Forms of a table

```xml
<fetch>
  <entity name="systemform">
    <attribute name="formid" />
    <attribute name="name" />
    <attribute name="type" />
    <attribute name="formactivationstate" />
    <attribute name="ismanaged" />
    <filter>
      <condition attribute="objecttypecode" operator="eq" value="1" />
    </filter>
    <order attribute="name" />
  </entity>
</fetch>
```

`formactivationstate` 0 is inactive (X10). `type` gives the dashboard subtype rule.

## Views of a table

```xml
<fetch>
  <entity name="savedquery">
    <attribute name="savedqueryid" />
    <attribute name="name" />
    <attribute name="querytype" />
    <attribute name="isdefault" />
    <filter>
      <condition attribute="returnedtypecode" operator="eq" value="1" />
    </filter>
    <order attribute="name" />
  </entity>
</fetch>
```

## Processes

```xml
<fetch>
  <entity name="workflow">
    <attribute name="workflowid" />
    <attribute name="name" />
    <attribute name="category" />
    <attribute name="primaryentity" />
    <attribute name="statecode" />
    <filter>
      <condition attribute="type" operator="eq" value="1" />
      <condition attribute="name" operator="like" value="%[Name]%" />
    </filter>
  </entity>
</fetch>
```

## Web resources

```xml
<fetch>
  <entity name="webresource">
    <attribute name="webresourceid" />
    <attribute name="name" />
    <attribute name="webresourcetype" />
    <attribute name="ismanaged" />
    <filter>
      <condition attribute="name" operator="like" value="[prefix]_/%" />
    </filter>
  </entity>
</fetch>
```

The same query, unfiltered by name or filtered to the names a form or ribbon references, gives the edit loop its list of known web resources.

## Sitemaps and apps

```xml
<fetch>
  <entity name="appmodule">
    <attribute name="appmoduleid" />
    <attribute name="uniquename" />
    <attribute name="name" />
    <link-entity name="appmodulecomponent" from="appmoduleidunique" to="appmoduleidunique" link-type="outer" alias="c">
      <attribute name="componenttype" />
      <attribute name="objectid" />
    </link-entity>
  </entity>
</fetch>
```

```xml
<fetch>
  <entity name="sitemap">
    <attribute name="sitemapid" />
    <attribute name="sitemapname" />
    <attribute name="sitemapnameunique" />
  </entity>
</fetch>
```

## Plug-in steps, custom APIs, connection references, roles

```xml
<fetch>
  <entity name="sdkmessageprocessingstep">
    <attribute name="sdkmessageprocessingstepid" />
    <attribute name="name" />
    <attribute name="stage" />
    <attribute name="statecode" />
  </entity>
</fetch>
```

The same shape works for `pluginassembly` (`pluginassemblyid`, `name`, `version`), `customapi` (`customapiid`, `uniquename`), `connectionreference` (`connectionreferenceid`, `connectionreferencelogicalname`), `environmentvariabledefinition` (`environmentvariabledefinitionid`, `schemaname`), `role` (`roleid`, `name`) and `fieldsecurityprofile` (`fieldsecurityprofileid`, `name`).

## Research — what already runs on a table

For planning and for checks before an edit: what is registered on a table now, whether it is in a solution or not. From plugin version 1.2026.11.1, `plugintype`, `sdkmessage`, `sdkmessagefilter`, `sdkmessageprocessingstepimage`, `customapirequestparameter` and `customapiresponseproperty` are on the configuration allow-list, so these queries run even with record queries switched off. An earlier version reports them as touching records: they need record queries enabled on the connection, and the approval dialog says so. When a query is refused for that reason, say what could not be checked.

**Custom plug-in steps on a table.** `primaryobjecttypecode` lives on the message filter, not on the step, and takes the table's type code. `customizationlevel` 1 leaves out the platform's internal steps — dozens on any table.

```xml
<fetch>
  <entity name="sdkmessageprocessingstep">
    <attribute name="sdkmessageprocessingstepid" />
    <attribute name="name" />
    <attribute name="stage" />
    <attribute name="mode" />
    <attribute name="rank" />
    <attribute name="filteringattributes" />
    <attribute name="statecode" />
    <filter>
      <condition attribute="customizationlevel" operator="eq" value="1" />
    </filter>
    <link-entity name="sdkmessagefilter" from="sdkmessagefilterid" to="sdkmessagefilterid" alias="f">
      <filter>
        <condition attribute="primaryobjecttypecode" operator="eq" value="[objectTypeCode]" />
      </filter>
    </link-entity>
    <link-entity name="sdkmessage" from="sdkmessageid" to="sdkmessageid" alias="m">
      <attribute name="name" />
    </link-entity>
    <link-entity name="plugintype" from="plugintypeid" to="eventhandler" link-type="outer" alias="t">
      <attribute name="typename" />
      <attribute name="assemblyname" />
    </link-entity>
  </entity>
</fetch>
```

**Images of a step.**

```xml
<fetch>
  <entity name="sdkmessageprocessingstepimage">
    <attribute name="name" />
    <attribute name="entityalias" />
    <attribute name="imagetype" />
    <attribute name="attributes" />
    <filter>
      <condition attribute="sdkmessageprocessingstepid" operator="eq" value="[stepid]" />
    </filter>
  </entity>
</fetch>
```

**Plug-in types of an assembly, and the steps registered on them** — the comparison a new build must pass before import (G2).

```xml
<fetch>
  <entity name="plugintype">
    <attribute name="plugintypeid" />
    <attribute name="typename" />
    <filter>
      <condition attribute="assemblyname" operator="eq" value="[AssemblyName]" />
    </filter>
    <link-entity name="sdkmessageprocessingstep" from="eventhandler" to="plugintypeid" link-type="outer" alias="s">
      <attribute name="name" />
    </link-entity>
  </entity>
</fetch>
```

**Custom APIs bound to a table.**

```xml
<fetch>
  <entity name="customapi">
    <attribute name="customapiid" />
    <attribute name="uniquename" />
    <attribute name="bindingtype" />
    <attribute name="boundentitylogicalname" />
    <attribute name="plugintypeid" />
    <filter>
      <condition attribute="boundentitylogicalname" operator="eq" value="[table]" />
    </filter>
  </entity>
</fetch>
```

**Processes on a table.** `primaryentity` takes the type code, not the logical name. `type` 1 keeps definitions and leaves out activations.

```xml
<fetch>
  <entity name="workflow">
    <attribute name="workflowid" />
    <attribute name="name" />
    <attribute name="category" />
    <attribute name="statecode" />
    <filter>
      <condition attribute="primaryentity" operator="eq" value="[objectTypeCode]" />
      <condition attribute="type" operator="eq" value="1" />
    </filter>
  </entity>
</fetch>
```

**Cloud flows are not found that way.** A cloud flow (`category` 5) keeps its trigger table in its definition, not in `primaryentity`, so the query above misses it. Search the definition for the table's logical name, with a small page size — definitions are large:

```xml
<fetch>
  <entity name="workflow">
    <attribute name="workflowid" />
    <attribute name="name" />
    <attribute name="statecode" />
    <filter>
      <condition attribute="category" operator="eq" value="5" />
      <condition attribute="clientdata" operator="like" value="%[table]%" />
    </filter>
  </entity>
</fetch>
```

A flow that only names the table in an action — a create or update from elsewhere — is a write path too. If the platform refuses the `like` on the definition, list the category 5 flows by name and ask the person which write to the table; never report "no flows" from the `primaryentity` query alone.
