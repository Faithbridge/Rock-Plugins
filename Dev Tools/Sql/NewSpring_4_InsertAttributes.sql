/* ====================================================== 
-- NewSpring Script #4: 
-- Inserts attributes.
  
--  Assumptions:
--  We only import attributes from the following group names:
    90 Day Tithing Challenge
    Discipleship - Grow
    Discipleship - Ownership
    Financial Planning
    Fuse
    KidSpring
    Salvation
    Stewardship
    Volunteer

   ====================================================== */
-- Make sure you're using the right Rock database:

USE Rock

/* ====================================================== */

-- Enable production mode for performance
SET NOCOUNT ON

-- Set the F1 database name
DECLARE @F1 nvarchar(255) = 'F1'

/* ====================================================== */
-- Start value lookups
/* ====================================================== */
declare @IsSystem int = 0, @Order int = 0,  @TextFieldTypeId int = 1, @True int = 1, @False int = 0 

declare @CampusFieldTypeId int, @DateFieldTypeId int, @BooleanFieldTypeId int, @PersonEntityTypeId int, @AttributeEntityTypeId int
select @CampusFieldTypeId = [Id] from FieldType where [Guid] = '1B71FEF4-201F-4D53-8C60-2DF21F1985ED'
select @DateFieldTypeId = [Id] from FieldType where [Guid] = '6B6AA175-4758-453F-8D83-FCD8044B5F36'
select @BooleanFieldTypeId = [Id] from FieldType where [Guid] = '1EDAFDED-DFE6-4334-B019-6EECBA89E05A'
select @PersonEntityTypeId = [Id] from EntityType where [Guid] = '72657ED8-D16E-492E-AC12-144C5E7567E7'
select @AttributeEntityTypeId = [Id] from EntityType where [Guid] = '5997C8D3-8840-4591-99A5-552919F90CBD'

/* ====================================================== */
-- Create attribute types
/* ====================================================== */
if object_id('tempdb..#attributes') is not null
begin
	drop table #attributes
end
create table #attributes (
	ID int IDENTITY(1,1),
	attributeGroupName nvarchar(255),
	attributeName nvarchar(255),	
	dateAttributeId int DEFAULT NULL,
	campusAttributeId int DEFAULT NULL,
	booleanAttributeId int DEFAULT NULL
)

insert into #attributes (attributeGroupName, attributeName)
select attribute_group_name, attribute_name
from f1..Attribute
where ( attribute_name like 'Ownership%Joined'
	or attribute_name like '%Tithing Challenge'
	or attribute_name like '%-%Baptism'
	or attribute_name like '%Financial Coaching'
	or attribute_name like '%-%Salvation'
	or attribute_name like '%Spring Zone'
	or attribute_name like '%Dinner Attendee'
	or attribute_name like 'Redirect Card%'
	or attribute_name like 'NewServe%'
)
group by attribute_group_name, attribute_name

/* ====================================================== */
-- Create attribute lookup
/* ====================================================== */
if object_id('tempdb..#attributeAssignment') is not null
begin
	drop table #attributeAssignment
end
create table #attributeAssignment (
	personId bigint,
	attributeId int,
	value nvarchar(255), 
	filterDate date
)

declare @scopeIndex int, @numItems int
select @scopeIndex = min(ID) from #attributes
select @numItems = count(1) + @scopeIndex from #attributes

while @scopeIndex <= @numItems
begin
	declare @AttributeGroup nvarchar(255), @AttributeName nvarchar(255), @AttributeCategoryId int, @DateAttributeId int, 
		@CampusAttributeId int, @BooleanAttributeId int, @campusGuid uniqueidentifier 
	
	select @AttributeGroup = attributeGroupName, @AttributeName = attributeName, @DateAttributeId = dateAttributeId, 
		@CampusAttributeId = campusAttributeId, @BooleanAttributeId = booleanAttributeId
	from #attributes where ID = @scopeIndex

	declare @msg nvarchar(max) = 'Starting ' + @AttributeGroup + ' / ' + @AttributeName 
	RAISERROR ( @msg, 0, 0 ) WITH NOWAIT

	if @AttributeGroup is not null
	begin
		
		-- set campus based on the attribute name
		select @campusGuid = [Guid] from Campus
		where shortcode = left(ltrim(@AttributeName), 3)
		or shortcode = right(rtrim(@AttributeName), 3) 

		-- depending on what attribute this is, take different actions
		-- Spring Zone is weird because it spans two GroupNames
		if @AttributeName = 'Spring Zone' and @DateAttributeId is null
		begin

			-- get or create the attribute category
			select @AttributeCategoryId = [Id] from Category
			where EntityTypeId = @AttributeEntityTypeId 
			and name = 'Child/Student Information'

			if @AttributeCategoryId is null
			begin
				insert Category ( IsSystem, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue, Name, [Description], [Order], [Guid] )
				select @IsSystem, @AttributeEntityTypeId, 'EntityTypeId', @PersonEntityTypeId, 'Stewardship', 'Attributes used for stewardship', @Order, NEWID()

				select @AttributeCategoryId = SCOPE_IDENTITY()
			end
			
			select @DateAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId and name = 'Is Special Needs'

			if @DateAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', 'IsSpecialNeeds', 'Is Special Needs', 
					'Whether or not the person has special needs.', '', @Order, @False, @False, @False, NEWID()

				select @DateAttributeId = SCOPE_IDENTITY()

				-- set additional attribute fields
				insert AttributeQualifier (IsSystem, AttributeId, [Key], Value, [Guid])
				select @IsSystem, @DateAttributeId, 'truetext', 'YES', NEWID()

				insert AttributeQualifier (IsSystem, AttributeId, [Key], Value, [Guid])
				select @IsSystem, @DateAttributeId, 'falsetext', 'No', NEWID()

				insert AttributeCategory (AttributeId, CategoryId)
				select @DateAttributeId, @AttributeCategoryId				
			end
		end
		-- end Spring Zone
		else if @AttributeGroup = '90 Day Tithing Challenge' and @DateAttributeId is null
		begin

			-- get or create the attribute category
			select @AttributeCategoryId = [Id] from Category
			where EntityTypeId = @AttributeEntityTypeId 
			and name = 'Stewardship'

			if @AttributeCategoryId is null
			begin
				insert Category ( IsSystem, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue, Name, [Description], [Order], [Guid] )
				select @IsSystem, @AttributeEntityTypeId, 'EntityTypeId', @PersonEntityTypeId, 'Stewardship', 'Attributes used for stewardship', @Order, NEWID()

				select @AttributeCategoryId = SCOPE_IDENTITY()
			end
				
			-- create attributes if they don't exist
			select @DateAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId and name = '90 DTC Date'

			if @DateAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', '90DTCDate', '90 DTC Date', 
					'The date this person signed up for the 90 Day Tithe Challenge.', '', @Order, @False, @False, @False, NEWID()
				
				select @DateAttributeId = SCOPE_IDENTITY()

				insert AttributeCategory (AttributeId, CategoryId)
				select @DateAttributeId, @AttributeCategoryId
			end

			select @CampusAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId and name = '90 DTC Campus'

			if @CampusAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @CampusFieldTypeId, @PersonEntityTypeId, '', '', '90DTCCampus', '90 DTC Campus', 
					'The campus where this person signed up for the 90 Day Tithe Challenge.', '', @Order, @False, @False, @False, NEWID()
			
				select @CampusAttributeId = SCOPE_IDENTITY()

				insert AttributeCategory (AttributeId, CategoryId)
				select @CampusAttributeId, @AttributeCategoryId
			end
		end	
		-- end 90 DTC
		else if @AttributeGroup = 'Discipleship - Grow' and @DateAttributeId is null
		begin

			-- get or create the attribute category
			select @AttributeCategoryId = [Id] from Category
			where EntityTypeId = @AttributeEntityTypeId 
			and name = 'Next Steps'

			if @AttributeCategoryId is null
			begin
				insert Category ( IsSystem, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue, Name, [Description], [Order], [Guid] )
				select @IsSystem, @AttributeEntityTypeId, 'EntityTypeId', @PersonEntityTypeId, 'Next Steps', 'Attributes used for next steps', @Order, NEWID()

				select @AttributeCategoryId = SCOPE_IDENTITY()
			end

			-- Baptism Date should already be created
			select @DateAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId and name = 'Baptism Date'

			if @DateAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', 'BaptismDate', 'Baptism Date', 
					'The date the person was baptized.', '', @Order, @False, @False, @False, NEWID()

				select @DateAttributeId = SCOPE_IDENTITY()

				insert AttributeCategory (AttributeId, CategoryId)
				select @DateAttributeId, @AttributeCategoryId
			end

			select @CampusAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId and name = 'Baptism Campus'

			if @CampusAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @CampusFieldTypeId, @PersonEntityTypeId, '', '', 'BaptismCampus', 'Baptism Campus', 
					'The campus where this person was baptized.', '', @Order, @False, @False, @False, NEWID()
			
				select @CampusAttributeId = SCOPE_IDENTITY()			

				insert AttributeCategory (AttributeId, CategoryId)
				select @CampusAttributeId, @AttributeCategoryId
			end	
		end 
		-- end Discipleship
		else if @AttributeGroup = 'Discipleship - Ownership' and @DateAttributeId is null
		begin
			
			-- get or create the attribute category
			select @AttributeCategoryId = [Id] from Category
			where EntityTypeId = @AttributeEntityTypeId 
			and name = 'Next Steps'

			if @AttributeCategoryId is null
			begin
				insert Category ( IsSystem, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue, Name, [Description], [Order], [Guid] )
				select @IsSystem, @AttributeEntityTypeId, 'EntityTypeId', @PersonEntityTypeId, 'Next Steps', 'Attributes used for next steps', @Order, NEWID()

				select @AttributeCategoryId = SCOPE_IDENTITY()
			end

			-- Membership Date should already be created
			select @DateAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId 
			and (name = 'Membership Date'
				or name = 'Ownership Date')

			if @DateAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', 'OwnershipDate', 'Ownership Date', 
					'The date the person became an owner.', '', @Order, @False, @False, @False, NEWID()

				select @DateAttributeId = SCOPE_IDENTITY()

				insert AttributeCategory (AttributeId, CategoryId)
				select @DateAttributeId, @AttributeCategoryId
			end		
		end 
		-- end Ownership
		else if @AttributeGroup = 'Fuse' and @DateAttributeId is null
		begin
			-- get or create the attribute category
			select @AttributeCategoryId = [Id] from Category
			where EntityTypeId = @AttributeEntityTypeId 
			and name = 'Next Steps'

			if @AttributeCategoryId is null
			begin
				insert Category ( IsSystem, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue, Name, [Description], [Order], [Guid] )
				select @IsSystem, @AttributeEntityTypeId, 'EntityTypeId', @PersonEntityTypeId, 'Next Steps', 'Attributes used for next steps', @Order, NEWID()

				select @AttributeCategoryId = SCOPE_IDENTITY()
			end

			-- either fuse salvation or fuse baptism
			if @AttributeName like '%Salvation'
			begin
				select @DateAttributeId = [Id] from Attribute 
				where EntityTypeId = @PersonEntityTypeId and name = 'Salvation Date'

				if @DateAttributeId is null
				begin
					insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
						[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
					select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', 'SalvationDate', 'Salvation Date', 
						'The date the person was saved.', '', @Order, @False, @False, @False, NEWID()

					select @DateAttributeId = SCOPE_IDENTITY()

					insert AttributeCategory (AttributeId, CategoryId)
					select @DateAttributeId, @AttributeCategoryId
				end

				select @CampusAttributeId = [Id] from Attribute 
				where EntityTypeId = @PersonEntityTypeId and name = 'Salvation Campus'

				if @CampusAttributeId is null
				begin
					insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
						[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
					select @IsSystem, @CampusFieldTypeId, @PersonEntityTypeId, '', '', 'SalvationCampus', 'Salvation Campus', 
						'The campus where this person was saved.', '', @Order, @False, @False, @False, NEWID()
			
					select @CampusAttributeId = SCOPE_IDENTITY()			

					insert AttributeCategory (AttributeId, CategoryId)
					select @CampusAttributeId, @AttributeCategoryId
				end

				select @BooleanAttributeId = [Id] from Attribute 
				where EntityTypeId = @PersonEntityTypeId and name = 'Fuse Salvation'

				if @BooleanAttributeId is null
				begin
					insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
						[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
					select @IsSystem, @BooleanFieldTypeId, @PersonEntityTypeId, '', '', 'FuseSalvation', 'Fuse Salvation', 
						'The salvation happened at Fuse.', '', @Order, @False, @False, @False, NEWID()
			
					select @BooleanAttributeId = SCOPE_IDENTITY()			

					-- set additional attribute fields
					insert AttributeQualifier (IsSystem, AttributeId, [Key], Value, [Guid])
					select @IsSystem, @BooleanAttributeId, 'truetext', 'YES', NEWID()

					insert AttributeQualifier (IsSystem, AttributeId, [Key], Value, [Guid])
					select @IsSystem, @BooleanAttributeId, 'falsetext', 'No', NEWID()

					insert AttributeCategory (AttributeId, CategoryId)
					select @BooleanAttributeId, @AttributeCategoryId
				end				
			end
			-- end fuse salvation
			else if @AttributeName like '%Baptism'
			begin
				select @DateAttributeId = [Id] from Attribute 
				where EntityTypeId = @PersonEntityTypeId and name = 'Baptism Date'

				if @DateAttributeId is null
				begin
					insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
						[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
					select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', 'BaptismDate', 'Baptism Date', 
						'The date the person was baptized.', '', @Order, @False, @False, @False, NEWID()

					select @DateAttributeId = SCOPE_IDENTITY()

					insert AttributeCategory (AttributeId, CategoryId)
					select @DateAttributeId, @AttributeCategoryId
				end

				select @CampusAttributeId = [Id] from Attribute 
				where EntityTypeId = @PersonEntityTypeId and name = 'Baptism Campus'

				if @CampusAttributeId is null
				begin
					insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
						[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
					select @IsSystem, @CampusFieldTypeId, @PersonEntityTypeId, '', '', 'BaptismCampus', 'Baptism Campus', 
						'The campus where this person was baptized.', '', @Order, @False, @False, @False, NEWID()
			
					select @CampusAttributeId = SCOPE_IDENTITY()			

					insert AttributeCategory (AttributeId, CategoryId)
					select @CampusAttributeId, @AttributeCategoryId
				end				
			end
			-- end fuse baptism
		end 
		-- end fuse
		else if @AttributeGroup = 'Salvation' and @DateAttributeId is null
		begin
			
			-- get or create the attribute category
			select @AttributeCategoryId = [Id] from Category
			where EntityTypeId = @AttributeEntityTypeId 
			and name = 'Next Steps'

			if @AttributeCategoryId is null
			begin
				insert Category ( IsSystem, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue, Name, [Description], [Order], [Guid] )
				select @IsSystem, @AttributeEntityTypeId, 'EntityTypeId', @PersonEntityTypeId, 'Next Steps', 'Attributes used for next steps', @Order, NEWID()

				select @AttributeCategoryId = SCOPE_IDENTITY()
			end

			select @DateAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId and name = 'Salvation Date'

			if @DateAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', 'SalvationDate', 'Salvation Date', 
					'The date the person was saved.', '', @Order, @False, @False, @False, NEWID()

				select @DateAttributeId = SCOPE_IDENTITY()

				insert AttributeCategory (AttributeId, CategoryId)
				select @DateAttributeId, @AttributeCategoryId
			end

			select @CampusAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId and name = 'Salvation Campus'

			if @CampusAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @CampusFieldTypeId, @PersonEntityTypeId, '', '', 'SalvationCampus', 'Salvation Campus', 
					'The campus where this person was saved.', '', @Order, @False, @False, @False, NEWID()
			
				select @CampusAttributeId = SCOPE_IDENTITY()			

				insert AttributeCategory (AttributeId, CategoryId)
				select @CampusAttributeId, @AttributeCategoryId
			end
		end 
		-- end Salvation
		else if @AttributeGroup = 'Stewardship' and @DateAttributeId is null
		begin
			
			-- get or create the attribute category
			select @AttributeCategoryId = [Id] from Category
			where EntityTypeId = @AttributeEntityTypeId 
			and name = 'Stewardship'

			if @AttributeCategoryId is null
			begin
				insert Category ( IsSystem, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue, Name, [Description], [Order], [Guid] )
				select @IsSystem, @AttributeEntityTypeId, 'EntityTypeId', @PersonEntityTypeId, 'Stewardship', 'Attributes used for stewardship', @Order, NEWID()

				select @AttributeCategoryId = SCOPE_IDENTITY()
			end

			select @DateAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId and name = 'Salvation Date'

			if @DateAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', 'SalvationDate', 'Salvation Date', 
					'The date the person was saved.', '', @Order, @False, @False, @False, NEWID()

				select @DateAttributeId = SCOPE_IDENTITY()

				insert AttributeCategory (AttributeId, CategoryId)
				select @DateAttributeId, @AttributeCategoryId
			end

			select @CampusAttributeId = [Id] from Attribute 
			where EntityTypeId = @PersonEntityTypeId and name = 'Salvation Campus'

			if @CampusAttributeId is null
			begin
				insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
					[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
				select @IsSystem, @CampusFieldTypeId, @PersonEntityTypeId, '', '', 'SalvationCampus', 'Salvation Campus', 
					'The campus where this person was saved.', '', @Order, @False, @False, @False, NEWID()
			
				select @CampusAttributeId = SCOPE_IDENTITY()			

				insert AttributeCategory (AttributeId, CategoryId)
				select @CampusAttributeId, @AttributeCategoryId
			end
		end 
		-- end Salvation
		else if @AttributeGroup = 'Volunteer' and @DateAttributeId is null
		begin
			
			-- get or create the attribute category
			select @AttributeCategoryId = [Id] from Category
			where EntityTypeId = @AttributeEntityTypeId 
			and name = 'New Serve'

			if @AttributeCategoryId is null
			begin
				insert Category ( IsSystem, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue, Name, [Description], [Order], [Guid] )
				select @IsSystem, @AttributeEntityTypeId, 'EntityTypeId', @PersonEntityTypeId, 'New Serve', 'Attributes used for new serve', @Order, NEWID()

				select @AttributeCategoryId = SCOPE_IDENTITY()
			end

			-- either a process start or a process redirect
			if @AttributeName like '%Start'
			begin

				select @DateAttributeId = [Id] from Attribute 
				where EntityTypeId = @PersonEntityTypeId and name = 'New Serve Start Date'

				if @DateAttributeId is null
				begin
					insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
						[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
					select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', 'NewServeStartDate', 'New Serve Start Date', 
						'The date the person started the New Serve process.', '', @Order, @False, @False, @False, NEWID()

					select @DateAttributeId = SCOPE_IDENTITY()

					insert AttributeCategory (AttributeId, CategoryId)
					select @DateAttributeId, @AttributeCategoryId
				end

				select @CampusAttributeId = [Id] from Attribute 
				where EntityTypeId = @PersonEntityTypeId and name = 'New Serve Start Campus'

				if @CampusAttributeId is null
				begin
					insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
						[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
					select @IsSystem, @CampusFieldTypeId, @PersonEntityTypeId, '', '', 'NewServeStartCampus', 'New Serve Start Campus', 
						'The campus the person started the new serve process.', '', @Order, @False, @False, @False, NEWID()
			
					select @CampusAttributeId = SCOPE_IDENTITY()			

					insert AttributeCategory (AttributeId, CategoryId)
					select @CampusAttributeId, @AttributeCategoryId
				end
			end
			-- end new serve start
			else if @AttributeName like '%Redirect'
			begin
				select @DateAttributeId = [Id] from Attribute 
				where EntityTypeId = @PersonEntityTypeId and name = 'New Serve Redirect Date'

				if @DateAttributeId is null
				begin
					insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
						[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
					select @IsSystem, @DateFieldTypeId, @PersonEntityTypeId, '', '', 'NewServeRedirectDate', 'New Serve Redirect Date', 
						'The date the person redirected the New Serve process.', '', @Order, @False, @False, @False, NEWID()

					select @DateAttributeId = SCOPE_IDENTITY()

					insert AttributeCategory (AttributeId, CategoryId)
					select @DateAttributeId, @AttributeCategoryId
				end

				select @CampusAttributeId = [Id] from Attribute 
				where EntityTypeId = @PersonEntityTypeId and name = 'New Serve Redirect Campus'

				if @CampusAttributeId is null
				begin
					insert Attribute ( [IsSystem], [FieldTypeId], [EntityTypeId], [EntityTypeQualifierColumn], [EntityTypeQualifierValue], 
						[Key], [Name], [Description], [DefaultValue], [Order], [IsGridColumn], [IsMultiValue], [IsRequired], [Guid] )
					select @IsSystem, @CampusFieldTypeId, @PersonEntityTypeId, '', '', 'NewServeRedirectCampus', 'New Serve Redirect Campus', 
						'The campus the person redirected the new serve process.', '', @Order, @False, @False, @False, NEWID()
			
					select @CampusAttributeId = SCOPE_IDENTITY()			

					insert AttributeCategory (AttributeId, CategoryId)
					select @CampusAttributeId, @AttributeCategoryId
				end
			end
			-- end new serve redirect
		end 
		-- end Salvation


		-- set attributes for use next time
		update #attributes 
		set dateAttributeId = @DateAttributeId,
			campusAttributeId = @CampusAttributeId,
			booleanAttributeId = @BooleanAttributeId
		where attributeGroupName = @AttributeGroup
			and attributeName = @AttributeName

		-- insert attribute values for dates
		if @DateAttributeId is not null
		begin
			insert into #attributeAssignment ( personId, attributeId, value, filterDate )
			select personId, @DateAttributeId, convert(nvarchar(50), a.start_date, 126), a.start_date
			from F1..Attribute a
			inner join PersonAlias pa
				on a.Individual_Id = pa.ForeignId
				and a.Attribute_Group_Name = @AttributeGroup 
				and a.Attribute_Name = @AttributeName
		end

		-- insert attribute values for campuses
		if @CampusAttributeId is not null
		begin
			insert into #attributeAssignment ( personId, attributeId, value, filterDate )			
			select personId, @CampusAttributeId, @campusGuid, a.start_date
			from F1..Attribute a
			inner join PersonAlias pa
				on a.Individual_Id = pa.ForeignId
				and a.Attribute_Group_Name = @AttributeGroup 
				and a.Attribute_Name = @AttributeName
		end

		-- insert attribute values for booleans
		if @BooleanAttributeId is not null
		begin
			insert into #attributeAssignment ( personId, attributeId, value, filterDate )
			select personId, @BooleanAttributeId, 'True', a.start_date
			from F1..Attribute a
			inner join PersonAlias pa
				on a.Individual_Id = pa.ForeignId
				and a.Attribute_Group_Name = @AttributeGroup 
				and a.Attribute_Name = @AttributeName
		end
	end
	-- end attribute not empty

	select @scopeIndex = @scopeIndex + 1
end
-- end while attribute loop

-- remove duplicate attributes and values
;WITH duplicates (personId, attributeId, id) 
AS (
    SELECT personId, attributeId, ROW_NUMBER() OVER (
		PARTITION BY personId, attributeId
		ORDER BY filterDate desc
    ) AS id
    FROM #attributeAssignment
)
delete from duplicates
where id > 1

-- insert attribute values
insert AttributeValue ( [IsSystem], [AttributeId], [EntityId], [Value], [Guid] )
select @IsSystem, attributeId, personId, value, NEWID()
from #attributeAssignment

-- clear the assignments for this attribute
--truncate table #attributeAssignment	

select attributeid, personid from #attributeAssignment where attributeid = 906 and personid = 188

select * from Rock..AttributeValue where entityid = 188
select * from rock..attributevalue where attributeid = 906


-- insert attendance for attribute of type Financial Coaching
/*
 
#TODO

*/

-- completed successfully
RAISERROR ( N'Completed successfully.', 0, 0 ) WITH NOWAIT

use master