# FetchXML for shaping

Every query goes through the plugin's `query` tool: explicit columns only (`<all-attributes />` is refused), no `top` (use `pageSize`), and results are paged — continue with `nextCursor` until it is `null` when the check needs every row. All tables below are on the plugin's configuration allow-list, so none of these touch records.

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
